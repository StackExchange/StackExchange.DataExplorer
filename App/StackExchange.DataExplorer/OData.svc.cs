using System;
using System.Collections.Generic;
using System.Data.EntityClient;
using System.Data.Metadata.Edm;
using System.Data.Services;
using System.Data.Services.Common;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Web;
using StackExchange.DataExplorer.Models.StackEntities;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer
{
    public class OData : DataService<Entities>
    {
        private const int ConnectionPoolSize = 10;
        private static readonly Regex _ipAddress = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static bool IsPrivateIP(string s) {
            return s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0.");
        }

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(NameValueCollection ServerVariables) {
            var ip = ServerVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = ServerVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue()) {
                ipForwarded = _ipAddress.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : "X.X.X.X";
        }


        public static string GetRemoteIP() {
            NameValueCollection ServerVaraibles;

            // This is a nasty hack so we don't crash the non-request test cases
            if (HttpContext.Current != null && HttpContext.Current.Request != null)
            {
                ServerVaraibles = HttpContext.Current.Request.ServerVariables;
            }
            else
            {
                ServerVaraibles = new NameValueCollection();
            }

            return GetRemoteIP(ServerVaraibles);
        }

        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config)
        {
            config.SetEntitySetAccessRule("*", EntitySetRights.AllRead);
            config.SetEntitySetPageSize("*", 50);
            config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
            config.UseVerboseErrors = true;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", 
            Justification = "If we dispose our connection our data source will be hosed")]
        protected override Entities CreateDataSource()
        {
            //throw new NotImplementedException("OData has been disabled while diagnosing a resource leak should be up by the 15th of August!");

            // var siteName = HttpContext.Current.Request.ServerVariables["ODATA_SITE"].ToLower();
            // YES, server vars would be nicer, but unfourtunatly pushing them through with rewrite,
            //   requires and edit to applicationHost.config, which is super duper hairy in azure.
            string siteName = HttpContext.Current.Request.Params["5D6DA575E16342AEB6AF9177FF673569"];

            if (siteName == null) {
                return null;
            }

            // how about only 1 request per 1 seconds - people are hammering this stuff like there 
            //  is no tomorrow

            string cacheKey = GetRemoteIP() + "_last_odata";
            DateTime? lastRequest = HttpContext.Current.Cache.Get(cacheKey) as DateTime?;

            if (lastRequest != null && (DateTime.Now - lastRequest.Value).TotalMilliseconds < 1000) {
                throw new InvalidOperationException("Sorry only one request per 1 second");
            }

            HttpContext.Current.Cache[cacheKey] = DateTime.Now;

            UriTemplateMatch match = WebOperationContext.Current.IncomingRequest.UriTemplateMatch;

            var builder = new UriBuilder(match.BaseUri);
            builder.Path = builder.Path.Replace("OData.svc", siteName + "/atom");
            Uri serviceUri = builder.Uri;
            OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRootUri"] = serviceUri;

            builder = new UriBuilder(match.RequestUri);
            builder.Path = builder.Path.Replace("odata.svc", siteName + "/atom");
            builder.Host = serviceUri.Host;
            OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRequestUri"] = builder.Uri;


            SqlConnection sqlConnection = Current.DB.Query<Site>("SELECT * FROM Sites WHERE LOWER(Name) = @siteName OR LOWER(TinyName) = @siteName", new {siteName}).First().GetConnection(ConnectionPoolSize);
            Current.RegisterConnectionForDisposal(sqlConnection);

            var workspace = new MetadataWorkspace(
                new[] {"res://*/"},
                new List<Assembly> {GetType().Assembly});
            var connection = new EntityConnection(workspace, sqlConnection);

            var entities = new Entities(connection);
            
            return entities;
        }

        protected override void OnStartProcessingRequest(ProcessRequestArgs args)
        {
            base.OnStartProcessingRequest(args);

            HttpCachePolicy c = HttpContext.Current.Response.Cache;
            c.SetCacheability(HttpCacheability.ServerAndPrivate);

            c.SetExpires(HttpContext.Current.Timestamp.AddSeconds(600));

            c.VaryByHeaders["Accept"] = true;
            c.VaryByHeaders["Accept-Charset"] = true;
            c.VaryByHeaders["Accept-Encoding"] = true;
            c.VaryByParams["*"] = true;

            // don't allow clients to mess with this. its valid period
            c.SetValidUntilExpires(true);
        }
    }
}