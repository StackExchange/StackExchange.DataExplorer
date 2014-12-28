using System.Web;
using System.Web.Mvc;
using System.Linq;
using StackExchange.Profiling;
using StackExchange.Profiling.MVCHelpers;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using StackExchange.DataExplorer.Controllers;

[assembly: WebActivator.PreApplicationStartMethod(
	typeof(StackExchange.DataExplorer.App_Start.MiniProfilerPackage), "PreStart")]

[assembly: WebActivator.PostApplicationStartMethod(
	typeof(StackExchange.DataExplorer.App_Start.MiniProfilerPackage), "PostStart")]


namespace StackExchange.DataExplorer.App_Start 
{
    public static class MiniProfilerPackage
    {
        class ProxySafeUserProvider : IUserProvider
        {
            public string GetUser(HttpRequest request)
            {
                return StackOverflowController.GetRemoteIP(request.ServerVariables);
            }
        }

        public static void PreStart()
        {
            WebRequestProfilerProvider.Settings.UserProvider = new ProxySafeUserProvider();
            MiniProfiler.Settings.SqlFormatter = new StackExchange.Profiling.SqlFormatters.SqlServerFormatter();

            var ignored = MiniProfiler.Settings.IgnoredPaths.ToList();

            ignored.Add("/assets/");

            MiniProfiler.Settings.IgnoredPaths = ignored.ToArray();

            //Make sure the MiniProfiler handles BeginRequest and EndRequest
            DynamicModuleUtility.RegisterModule(typeof(MiniProfilerStartupModule));

            // Profile views
            var copy = ViewEngines.Engines.ToList();
            ViewEngines.Engines.Clear();
            foreach (var item in copy)
            {
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
            }

            //Setup profiler for Controllers via a Global ActionFilter
            GlobalFilters.Filters.Add(new ProfilingActionFilter());

            MiniProfiler.Settings.Results_List_Authorize = request => 
            {
                return Current.User.IsAdmin;
            };
        }

        public static void PostStart()
        {
            // Intercept ViewEngines to profile all partial views and regular views.
            // If you prefer to insert your profiling blocks manually you can comment this out
            var copy = ViewEngines.Engines.ToList();
            ViewEngines.Engines.Clear();
            foreach (var item in copy)
            {
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
            }
        }
    }

    public class MiniProfilerStartupModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
            {
                MiniProfiler.Start();
            };

            context.EndRequest += (sender, e) =>
            {
                MiniProfiler.Stop();
            };
        }

        public void Dispose() { }
    }
}

