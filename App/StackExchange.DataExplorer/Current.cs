using System;
using System.Runtime.Remoting.Messaging;
using System.Web;
using System.Web.Caching;
using StackExchange.DataExplorer.Controllers;
using StackExchange.DataExplorer.Models;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using StackExchange.Profiling;


namespace StackExchange.DataExplorer
{
    /// <summary>
    /// Helper class that provides quick access to common objects used across a single request.
    /// </summary>
    public static class Current
    {
        private static DeploymentTier? _tier;

        const string DISPOSE_CONNECTION_KEY = "dispose_connections";

        public static Recaptcha.RecaptchaControl NewRecaptchControl()
        {

            Recaptcha.RecaptchaControl control = new Recaptcha.RecaptchaControl();
            control.PrivateKey = AppSettings.RecaptchaPrivateKey;
            control.PublicKey = AppSettings.RecaptchaPublicKey;
            control.Theme = "clean";

            if (!Context.Request.IsSecureConnection)
            {
                var forwarded = Context.Request.Headers["X-Forwarded-Proto"];
                control.OverrideSecureMode = forwarded != null && forwarded.StartsWith("https");
            }

            return control;
        }

        public static void RegisterConnectionForDisposal(SqlConnection connection)
        {
            List<SqlConnection> connections = Context.Items[DISPOSE_CONNECTION_KEY] as List<SqlConnection>;
            if (connections == null)
            {
                Context.Items[DISPOSE_CONNECTION_KEY] = connections  = new List<SqlConnection>();
            }

            connections.Add(connection);
        }

        public static void DisposeRegisteredConnections()
        {
            List<SqlConnection> connections = Context.Items[DISPOSE_CONNECTION_KEY] as List<SqlConnection>;
            if (connections != null)
            {
                Context.Items[DISPOSE_CONNECTION_KEY] = null;

                foreach (var connection in connections)
                {
                    try
                    {
                        if (connection.State != ConnectionState.Closed) {
                            GlobalApplication.LogException("Connection was not in a closed state.");
                        }

                        connection.Dispose();
                    }
                    catch { 
                        /* don't care, nothing we can do */
                    }
                }
            }
        }

        /// <summary>
        /// Shortcut to HttpContext.Current.
        /// </summary>
        public static HttpContext Context
        {
            get { return HttpContext.Current; }
        }

        /// <summary>
        /// Shortcut to HttpContext.Current.Request.
        /// </summary>
        public static HttpRequest Request
        {
            get { return Context.Request; }
        }

        /// <summary>
        /// Is this request is over HTTPS (or behind an HTTPS load balancer)?
        /// </summary>
        public static bool IsSecureConnection
        {
            get
            {
                return Request.IsSecureConnection ||
                    // This can be "http", "https", or the more fun "https, http, https, https" even.
                       (Request.Headers["X-Forwarded-Proto"] != null &&
                        Request.Headers["X-Forwarded-Proto"].StartsWith("https"));
            }
        }

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


        class ErrorLoggingProfiler : Profiling.Data.IDbProfiler
        {
            Profiling.Data.IDbProfiler wrapped;

            public ErrorLoggingProfiler(Profiling.Data.IDbProfiler wrapped)
            {
                this.wrapped = wrapped;
            }

            public void ExecuteFinish(IDbCommand profiledDbCommand, Profiling.Data.SqlExecuteType executeType, DbDataReader reader)
            {
                this.wrapped.ExecuteFinish(profiledDbCommand, executeType, reader);
            }

            public void ExecuteStart(IDbCommand profiledDbCommand, Profiling.Data.SqlExecuteType executeType)
            {
                this.wrapped.ExecuteStart(profiledDbCommand, executeType);
            }

            public bool IsActive
            {
                get { return this.wrapped.IsActive; }
            }

            public void OnError(IDbCommand profiledDbCommand, Profiling.Data.SqlExecuteType executeType, Exception exception)
            {
                var formatter = new Profiling.SqlFormatters.SqlServerFormatter();
                exception.Data["SQL"] = formatter.FormatSql(profiledDbCommand.CommandText, SqlTiming.GetCommandParameters(profiledDbCommand));
                this.wrapped.OnError(profiledDbCommand, executeType, exception);
            }

            public void ReaderFinish(IDataReader reader)
            {
                this.wrapped.ReaderFinish(reader);
            }
        }

        /// <summary>
        /// Gets the single data context for this current request.
        /// </summary>
        public static DataExplorerDatabase DB
        {
            get
            {
                DataExplorerDatabase result = null;
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
                    DbConnection cnn = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["AppConnection"].ConnectionString);
                    if (Profiler != null)
                        cnn = new Profiling.Data.ProfiledDbConnection(cnn, new ErrorLoggingProfiler(Profiler));
                    cnn.Open();
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
        /// Gets where this code is running, e.g. Prod, Dev
        /// </summary>
        public static DeploymentTier Tier
        {
            get
            {
                if (!_tier.HasValue)
                    _tier = DeploymentTier.Local;
                //_tier = (DeploymentTier) Enum.Parse(typeof(DeploymentTier), Site.Tier, true);

                return _tier.Value;
            }
        }

        /// <summary>
        /// Allows end of reqeust code to clean up this request's DB.
        /// </summary>
        public static void DisposeDB()
        {
            DataExplorerDatabase db = null;
            if (Context != null)
            {
                db = Context.Items["DB"] as DataExplorerDatabase;
            }
            else
            {
                db = CallContext.GetData("DB") as DataExplorerDatabase;
            }
            if (db != null)
            {
                db.Dispose();
                if (Context != null)
                {
                    Context.Items["DB"] = null;
                }
                else
                {
                    CallContext.SetData("DB", null);
                }
            }
        }

        /// <summary>
        /// retrieve an integer from the HttpRuntime.Cache; returns 0 if value does not exist
        /// </summary>
        public static int GetCachedInt(string key)
        {
            object o = HttpRuntime.Cache[key];
            if (o == null) return 0;
            return (int)o;
        }

        /// <summary>
        /// remove a cached object from the HttpRuntime.Cache
        /// </summary>
        public static void RemoveCachedObject(string key)
        {
            HttpRuntime.Cache.Remove(key);
        }

        /// <summary>
        /// retrieve an object from the HttpRuntime.Cache
        /// </summary>
        public static object GetCachedObject(string key)
        {
            return HttpRuntime.Cache[key];
        }

        /// <summary>
        /// add an object to the HttpRuntime.Cache with an absolute expiration time
        /// </summary>
        public static void SetCachedObject(string key, object o, int durationSecs)
        {
            HttpRuntime.Cache.Add(
                key,
                o,
                null,
                DateTime.Now.AddSeconds(durationSecs),
                Cache.NoSlidingExpiration,
                CacheItemPriority.High,
                null);
        }

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
        /// add a non-removable, non-expiring object to the HttpRuntime.Cache
        /// </summary>
        public static void SetCachedObjectPermanent(string key, object o)
        {
            HttpRuntime.Cache.Remove(key);
            HttpRuntime.Cache.Add(
                key,
                o,
                null,
                Cache.NoAbsoluteExpiration,
                Cache.NoSlidingExpiration,
                CacheItemPriority.NotRemovable,
                null);
        }

        /// <summary>
        /// retrieves a string from the HttpContext.Cache, or null if the key doesn't exist
        /// </summary>
        public static string GetCachedString(string key)
        {
            object o = HttpRuntime.Cache[key];
            if (o != null) return o.ToString();
            return null;
        }

        /// <summary>
        /// places a string in the HttpContext.Cache
        /// cached with "sliding expiration", so will only be deleted if NOT accessed for durationSecs
        /// </summary>
        public static void SetCachedString(string key, int durationSecs, string s)
        {
            HttpRuntime.Cache.Add(
                key,
                s,
                null,
                DateTime.MaxValue,
                TimeSpan.FromSeconds(durationSecs),
                CacheItemPriority.High,
                null);
        }

        /// <summary>
        /// manually write a message (wrapped in a simple Exception) to our standard exception log
        /// </summary>
        public static void LogException(string message, Exception inner = null)
        {
            if (inner != null)
                GlobalApplication.LogException(new Exception(message, inner));
            else 
                GlobalApplication.LogException(message);
        }

        /// <summary>
        /// manually write an exception to our standard exception log
        /// </summary>
        public static void LogException(Exception ex)
        {
            GlobalApplication.LogException(ex);
        }


        public static string GoogleAnalytics
        {
            get
            {
#if DEBUG
                return "";
#else
   return @"<script type=""text/javascript"">

  var _gaq = _gaq || [];
  _gaq.push(['_setAccount', 'UA-50203-8']);
  _gaq.push(['_trackPageview']);

  (function() {
    var ga = document.createElement('script'); ga.type = 'text/javascript'; ga.async = true;
    ga.src = ('https:' == document.location.protocol ? 'https://ssl' : 'http://www') + '.google-analytics.com/ga.js';
    var s = document.getElementsByTagName('script')[0]; s.parentNode.insertBefore(ga, s);
  })();

</script>"; 
   
#endif
            }
        }

        public static StackExchange.Profiling.MiniProfiler Profiler 
        { 
            get 
            {
                return StackExchange.Profiling.MiniProfiler.Current;
            } 
        }
    }

    public enum DeploymentTier
    {
        Prod,
        Dev,
        Local
    }
}