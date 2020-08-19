using System;
using System.Runtime.Remoting.Messaging;
using System.Web;
using System.Web.Caching;
using StackExchange.DataExplorer.Controllers;
using StackExchange.DataExplorer.Models;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using StackExchange.Exceptional;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;
using StackExchange.Profiling.SqlFormatters;


namespace StackExchange.DataExplorer
{
    /// <summary>
    /// Helper class that provides quick access to common objects used across a single request.
    /// </summary>
    public static class Current
    {
        const string DISPOSE_CONNECTION_KEY = "dispose_connections";

        public static void RegisterConnectionForDisposal(SqlConnection connection)
        {
            var connections = Context.Items[DISPOSE_CONNECTION_KEY] as List<SqlConnection>;
            if (connections == null)
            {
                Context.Items[DISPOSE_CONNECTION_KEY] = connections  = new List<SqlConnection>();
            }

            connections.Add(connection);
        }

        public static void DisposeRegisteredConnections()
        {
            var connections = Context.Items[DISPOSE_CONNECTION_KEY] as List<SqlConnection>;
            if (connections == null) return;

            Context.Items[DISPOSE_CONNECTION_KEY] = null;
            foreach (var connection in connections)
            {
                try
                {
                    if (connection.State != ConnectionState.Closed)
                    {
                        LogException("Connection was not in a closed state.");
                    }
                    connection.Dispose();
                }
                catch { /* don't care, nothing we can do */ }
            }
        }

        /// <summary>
        /// Shortcut to HttpContext.Current.
        /// </summary>
        public static HttpContext Context => HttpContext.Current;

        /// <summary>
        /// Shortcut to HttpContext.Current.Request.
        /// </summary>
        public static HttpRequest Request => Context.Request;

        /// <summary>
        /// Is this request is over HTTPS (or behind an HTTPS load balancer)?
        /// </summary>
        /// <remarks>
        /// This can be "http", "https", or the more fun "https, http, https, https" even.
        /// </remarks>
        public static bool IsSecureConnection =>
            Request.IsSecureConnection ||
            (Request.Headers["X-Forwarded-Proto"]?.StartsWith("https") ?? false);

        /// <summary>
        /// Gets the controller for the current request; should be set during init of current request's controller.
        /// </summary>
        public static StackOverflowController Controller
        {
            get { return Context.Items["Controller"] as StackOverflowController; }
            set { Context.Items["Controller"] = value; }
        }

        /// <summary>
        /// Gets the current "authenticated" user from this request's controller.
        /// </summary>
        public static User User
        {
            get 
            {
                if (Controller == null)
                {
                    return StackOverflowController.GetCurrentUser(HttpContext.Current.Request, HttpContext.Current.User.Identity.Name);
                }
                return Controller.CurrentUser; 
            }
        }


        class ErrorLoggingProfiler : IDbProfiler
        {
            private readonly IDbProfiler _wrapped;

            public ErrorLoggingProfiler(IDbProfiler wrapped)
            {
                _wrapped = wrapped;
            }

            public void ExecuteFinish(IDbCommand profiledDbCommand, SqlExecuteType executeType, DbDataReader reader)
            {
                _wrapped.ExecuteFinish(profiledDbCommand, executeType, reader);
            }

            public void ExecuteStart(IDbCommand profiledDbCommand, SqlExecuteType executeType)
            {
                _wrapped.ExecuteStart(profiledDbCommand, executeType);
            }

            public bool IsActive => _wrapped.IsActive;

            public void OnError(IDbCommand profiledDbCommand, SqlExecuteType executeType, Exception exception)
            {
                var formatter = new SqlServerFormatter();
                exception.Data["SQL"] = formatter.FormatSql(profiledDbCommand.CommandText, SqlTiming.GetCommandParameters(profiledDbCommand));
                _wrapped.OnError(profiledDbCommand, executeType, exception);
            }

            public void ReaderFinish(IDataReader reader)
            {
                _wrapped.ReaderFinish(reader);
            }
        }

        /// <summary>
        /// Gets the single data context for this current request.
        /// </summary>
        public static DataExplorerDatabase DB
        {
            get
            {
                DataExplorerDatabase result;
                if (Context != null)
                {
                    result = Context.Items["DB"] as DataExplorerDatabase;
                }
                else
                {
                    // unit tests
                    result = CallContext.GetData("DB") as DataExplorerDatabase;
                }

                if (result == null)
                {
                    DbConnection cnn = new SqlConnection(ConfigurationManager.ConnectionStrings["AppConnection"].ConnectionString);

                    var profiler = MiniProfiler.Current;
                    if (profiler != null)
                    {
                        cnn = new ProfiledDbConnection(cnn, new ErrorLoggingProfiler(profiler));
                    }

                    result = DataExplorerDatabase.Create(cnn, 30);
                    if (Context != null)
                    {
                        Context.Items["DB"] = result;
                    }
                    else
                    {
                        CallContext.SetData("DB", result);
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Allows end of reqeust code to clean up this request's DB.
        /// </summary>
        public static void DisposeDB()
        {
            if (Context != null)
            {
                var db = Context.Items["DB"] as DataExplorerDatabase;
                db?.Dispose();
                Context.Items["DB"] = null;
            }
            // Also clear the call context DB if we ever hit it in a background thread
            {
                var db = CallContext.GetData("DB") as DataExplorerDatabase;
                db?.Dispose();
                CallContext.SetData("DB", null);
            }
        }

        /// <summary>
        /// retrieve an object from the HttpRuntime.Cache
        /// </summary>
        public static object GetCachedObject(string key) => HttpRuntime.Cache[key];
        
        /// <summary>
        /// add an object to the HttpRuntime.Cache with a sliding expiration time
        /// </summary>
        public static void SetCachedObjectSliding(string key, object o, int slidingSecs)
        {
            HttpRuntime.Cache.Add(
                key,
                o,
                null,
                Cache.NoAbsoluteExpiration,
                new TimeSpan(0, 0, slidingSecs),
                CacheItemPriority.High,
                null);
        }

        /// <summary>
        /// manually write a message (wrapped in a simple Exception) to our standard exception log
        /// </summary>
        public static void LogException(string message, Exception inner = null) =>
            LogException(inner != null ? new Exception(message, inner) : new Exception(message));

        /// <summary>
        /// manually write an exception to our standard exception log
        /// </summary>
        public static void LogException(Exception ex, bool rollupPerServer = false)
        {
            try
            {
                ErrorStore.LogException(ex, Context, appendFullStackTrace: true, rollupPerServer: rollupPerServer);
            }
            catch { /* Do nothing */ }
        }
        
        public static string GoogleAnalytics
        {
            get
            {
#if DEBUG
                return "";
#else
   return @"<script>
(function(i,s,o,g,r,a,m){i['GoogleAnalyticsObject']=r;i[r]=i[r]||function(){
(i[r].q=i[r].q||[]).push(arguments)},i[r].l=1*new Date();a=s.createElement(o),
m=s.getElementsByTagName(o)[0];a.async=1;a.src=g;m.parentNode.insertBefore(a,m)
})(window,document,'script','https://www.google-analytics.com/analytics.js','ga');

ga('create', 'UA-50203-8', 'auto');
ga('send', 'pageview');
</script>";
#endif
            }
        }

        /// <summary>
        /// When a client IP can't be determined
        /// </summary>
        public const string UnknownIP = "0.0.0.0";

        private static readonly Regex _ipAddress = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string s) => 
            s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0.");

        /// <summary>
        /// Answers the current request's user's ip address; checks for any forwarding proxy
        /// </summary>
        public static string RemoteIP => GetRemoteIP(Request.ServerVariables);

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(NameValueCollection serverVariables, string unknownIP = UnknownIP)
        {
            string ip = serverVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            string ipForwarded = serverVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue())
            {
                ipForwarded = _ipAddress.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : unknownIP;
        }
    }
}