using System;
using System.Web;
using System.Web.Mvc;
using System.Linq;
using StackExchange.DataExplorer.Controllers;
using StackExchange.Profiling;
using StackExchange.Profiling.Mvc;
using StackExchange.Profiling.Storage;

namespace StackExchange.DataExplorer
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

        public static void Start()
        {
            WebRequestProfilerProvider.Settings.UserProvider = new ProxySafeUserProvider();
            MiniProfiler.Settings.SqlFormatter = new Profiling.SqlFormatters.SqlServerFormatter();
            MiniProfiler.Settings.Storage = new HttpRuntimeCacheStorage(TimeSpan.FromMinutes(20));

            var ignored = MiniProfiler.Settings.IgnoredPaths.ToList();
            ignored.Add("/assets/");
            MiniProfiler.Settings.IgnoredPaths = ignored.ToArray();

            // Profile views
            var copy = ViewEngines.Engines.ToList();
            ViewEngines.Engines.Clear();
            foreach (var item in copy)
            {
                ViewEngines.Engines.Add(new ProfilingViewEngine(item));
            }

            //Setup profiler for Controllers via a Global ActionFilter
            GlobalFilters.Filters.Add(new ProfilingActionFilter());

            MiniProfiler.Settings.Results_List_Authorize = request => Current.User.IsAdmin;
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
}

