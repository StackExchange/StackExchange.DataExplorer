using System;
using System.Reflection;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using StackExchange.DataExplorer.Helpers;
using StackExchange.Profiling;

namespace StackExchange.DataExplorer
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class GlobalApplication : HttpApplication
    {
        public static string AppRevision
        {
            get
            {
                var s = HttpContext.Current.Application[nameof(AppRevision)] as string;
                if (string.IsNullOrEmpty(s))
                {
                    s = Assembly.GetAssembly(typeof (GlobalApplication)).GetName().Version.ToString(4);
                    HttpContext.Current.Application[nameof(AppRevision)] = s;
                }
                return s;
            }
        }

        protected void Application_Start(object sender, EventArgs e)
        {
            // disable the X-AspNetMvc-Version: header
            MvcHandler.DisableMvcResponseHeader = true;

            // set up MVC routes so our app URLs actually work
            // IMPORTANT: this must be called last; nothing else appears to execute after this
            RegisterRoutes(RouteTable.Routes);
            BundleConfig.Start();
            MiniProfilerPackage.Start();
        }

        /// <summary>
        /// register our ASP.NET MVC routes
        /// </summary>
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("errors");
            routes.IgnoreRoute("errors/{*pathInfo}");
            routes.IgnoreRoute("{*allaspx}", new {allaspx = @".*\.aspx(/.*)?"});
            routes.IgnoreRoute("{*allaxd}", new {allaxd = @".*\.axd(/.*)?"});
            routes.IgnoreRoute("favicon.ico");
            routes.IgnoreRoute("assets/{*pathInfo}");

            StackRouteAttribute.MapDecoratedRoutes(routes);

            // MUST be the last route as a catch-all!
            routes.MapRoute("{*url}", new {controller = "Error", action = "PageNotFound"});
        }


        protected void Application_Error(object sender, EventArgs e)
        {
            Application["LastError"] = Server.GetLastError();

#if DEBUG
            var lastError = Server.GetLastError();
            string sql = null;

            try
            {
                sql = lastError.Data["SQL"] as string;
            }
            catch
            {
                // skip it
            }

            if (sql == null) return;

            var ex = new HttpUnhandledException("An unhandled exception occurred during the execution of the current web request. Please review the stack trace for more information about the error and where it originated in the code.", lastError);

            Server.ClearError();

            var html = ex.GetHtmlErrorMessage();
            var traceNode = "<b>Stack Trace:</b>";
            html = html.Replace(traceNode, @"<b>Sql:</b><br><br>
    <table width='100%' bgcolor='#ffffccc'>
    <tbody><tr><td><code><pre>" + sql + @"</pre></code></td></tr></tbody>
    </table><br>" + traceNode);

            HttpContext.Current.Response.Write(html);
            HttpContext.Current.Response.StatusCode = 500;
            HttpContext.Current.Response.Status = "Internal Server Error";
            HttpContext.Current.Response.End();
#endif  
        }

        protected void Application_BeginRequest()
        {
            // Incoming IP handling isn't correct - disabling MiniProfiler until a 4.1.0 upgrade and proper decoding
            //MiniProfiler.Start();
        }

        protected void Application_EndRequest()
        {
            Current.DisposeDB();
            Current.DisposeRegisteredConnections();
            //MiniProfiler.Stop();
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            if (HttpContext.Current.User == null) return;
            if (!HttpContext.Current.User.Identity.IsAuthenticated) return;
            var id = HttpContext.Current.User.Identity as FormsIdentity;
            if (id == null) return;

            var ticket = id.Ticket;
            string[] roles = (ticket.UserData ?? "").Split(',');
            HttpContext.Current.User = new GenericPrincipal(id, roles);
        }
    }
}
