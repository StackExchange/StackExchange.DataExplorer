using System;
using System.Reflection;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using SimpleErrorHandler;
using StackExchange.DataExplorer.Helpers;
using System.Linq;
using MvcMiniProfiler.MVCHelpers;

namespace StackExchange.DataExplorer
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class GlobalApplication : HttpApplication
    {
        private static ErrorLogModule ErrorModule;

        public static string AppRevision
        {
            get
            {
                var s = HttpContext.Current.Application["AppRevision"] as string;
                if (string.IsNullOrEmpty(s))
                {
                    s = Assembly.GetAssembly(typeof (GlobalApplication)).GetName().Version.ToString(4);
                    HttpContext.Current.Application["AppRevision"] = s;
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

            var copy = ViewEngines.Engines.ToList();
            ViewEngines.Engines.Clear();
            foreach (var item in copy)
            {
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
            }

            GlobalFilters.Filters.Add(new ProfilingActionFilter());
        }

        // http://msdn.microsoft.com/en-us/library/system.web.httpapplication.init(VS.71).aspx
        public override void Init()
        {
            base.Init();

            // Get our error handler, so we can write exceptions
            ErrorModule = Modules["ErrorLog"] as ErrorLogModule; // this requires full trust
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

            RouteAttribute.MapDecoratedRoutes(routes);

            // MUST be the last route as a catch-all!
            routes.MapRoute("{*url}", new {controller = "Error", action = "PageNotFound"});

        }


        protected void Application_Error(object sender, EventArgs e)
        {
            // TODO: yes, this might be .. a bad idea, but I'm trying it for now
            Application["LastError"] = Server.GetLastError();
        }


        protected void Application_EndRequest(object sender, EventArgs e)
        {
            Current.DisposeDB();
            Current.DisposeRegisteredConnections();
        }


        /// <summary>
        /// manually write a message (wrapped in a simple Exception) to our standard exception log
        /// </summary>
        public static void LogException(string message)
        {
            LogException(new Exception(message));
        }

        /// <summary>
        /// manually write an exception to our standard exception log
        /// </summary>
        public static void LogException(Exception ex)
        {
            try
            {
                if (ErrorModule != null)
                    ErrorModule.LogException(ex, HttpContext.Current);
            }
            catch
            {
                /* Do nothing */
            }
        }


        protected void Application_AuthenticateRequest(Object sender, EventArgs e)
        {
            if (HttpContext.Current.User != null)
            {
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    if (HttpContext.Current.User.Identity is FormsIdentity)
                    {
                        // let authenticated users get a taste of our awesome profiling
            
                        var id = (FormsIdentity) HttpContext.Current.User.Identity;
                        FormsAuthenticationTicket ticket = id.Ticket;

                        string[] roles = (ticket.UserData ?? "").Split(',');
                        HttpContext.Current.User = new GenericPrincipal(id, roles);
                        return;
                    }
                }
            }

           // profiling for everyone.
           //MvcMiniProfiler.MiniProfiler.Stop(discardResults: true);
        }
    }
}