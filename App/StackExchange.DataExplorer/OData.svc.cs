using System;
using System.Collections.Generic;
using System.Data.EntityClient;
using System.Data.Metadata.Edm;
using System.Data.Services;
using System.Data.Services.Common;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Web;
using StackExchange.DataExplorer.Models.StackEntities;
using System.Reflection;
using System.Data.SqlClient;


namespace StackExchange.DataExplorer
{

    public class OData : DataService<Entities>
    {
        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config)
        {
           config.SetEntitySetAccessRule("*", EntitySetRights.AllRead);
           config.SetEntitySetPageSize("*", 50);
           config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
        }


        protected override Entities CreateDataSource() {

            // var siteName = HttpContext.Current.Request.ServerVariables["ODATA_SITE"].ToLower();
            // YES, server vars would be nicer, but unfourtunatly pushing them through with rewrite,
            //   requires and edit to applicationHost.config, which is super duper hairy in azure.
            var siteName = HttpContext.Current.Request.Params["5D6DA575E16342AEB6AF9177FF673569"];

            UriTemplateMatch match = WebOperationContext.Current.IncomingRequest.UriTemplateMatch;

            UriBuilder builder = new UriBuilder(match.BaseUri);
            builder.Path = builder.Path.Replace("OData.svc", siteName + "/o");
            var serviceUri = builder.Uri;
            OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRootUri"] = serviceUri;

            builder = new UriBuilder(match.RequestUri);
            builder.Path = builder.Path.Replace("odata.svc", siteName + "/o");
            builder.Host = serviceUri.Host;
            OperationContext.Current.IncomingMessageProperties["MicrosoftDataServicesRequestUri"] = builder.Uri; 

            
            SqlConnection sqlConnection = Current.DB.Sites.First(s => s.Name.ToLower() == siteName).GetConnection();

            MetadataWorkspace workspace = new MetadataWorkspace(
                new string[] { "res://*/" }, 
                new List<Assembly>() { this.GetType().Assembly });
            EntityConnection connection = new EntityConnection(workspace, sqlConnection);

            Entities entities = new Entities(connection);
            return entities;
        }


        protected override void OnStartProcessingRequest(ProcessRequestArgs args)
        {
            base.OnStartProcessingRequest(args);
            HttpContext context = HttpContext.Current;

            HttpCachePolicy c = HttpContext.Current.Response.Cache;
            c.SetCacheability(HttpCacheability.ServerAndPrivate);

            c.SetExpires(HttpContext.Current.Timestamp.AddSeconds(600));

            c.VaryByHeaders["Accept"] = true;
            c.VaryByHeaders["Accept-Charset"] = true;
            c.VaryByHeaders["Accept-Encoding"] = true;
            c.VaryByParams["*"] = true;

        }

    }
 
}
