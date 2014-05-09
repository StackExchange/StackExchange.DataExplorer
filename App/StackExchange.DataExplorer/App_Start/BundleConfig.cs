using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;

[assembly: WebActivator.PreApplicationStartMethod(
    typeof(StackExchange.DataExplorer.App_Start.BundleConfig), "PreStart")]

namespace StackExchange.DataExplorer.App_Start
{
    public class BundleConfig
    {
        public static void PreStart()
        {
            // register public resource bundles (css/js)
#if !DEBUG
            BundleTable.EnableOptimizations = true;
#endif

            RegisterBundles(BundleTable.Bundles);
        }

        private static void RegisterBundles(BundleCollection bundles)
        {

#if !DEBUG            
            bundles.UseCdn = true;
#endif

            bundles.Add(new ScriptBundle("~/assets/js/jquery", "//ajax.googleapis.com/ajax/libs/jquery/1.7.1/jquery.min.js")
                .Include("~/Scripts/jquery-{version}.js")
            );

            bundles.Add(new ScriptBundle("~/assets/js/master")
                .Include("~/Scripts/master.js")
                .Include("~/Scripts/jquery.autocomplete.js")
            );

            bundles.Add(new ScriptBundle("~/assets/js/query")
                .Include("~/Scripts/date.js")
                .Include("~/Scripts/jquery.textarearesizer.js")
                .Include("~/Scripts/jquery.event.drag-2.0.js")
                .Include("~/Scripts/slick.core.js")
                .Include("~/Scripts/slick.grid.js")
                .Include("~/Scripts/codemirror/codemirror.js")
                .Include("~/Scripts/codemirror/sql.js")
                .Include("~/Scripts/codemirror/runmode.js")
                .Include("~/Scripts/qp.js")
                .Include("~/Scripts/query.parameterparser.js")
                .Include("~/Scripts/query.siteswitcher.js")
                .Include("~/Scripts/query.js")
            );

            bundles.Add(new ScriptBundle("~/assets/js/editor")
                .Include("~/Scripts/query.sidebar.js")
                .Include("~/Scripts/query.tablehelpers.js")
            );

            bundles.Add(new ScriptBundle("~/assets/js/flot")
                .Include("~/Scripts/flot/jquery.flot.js")
                .Include("~/Scripts/flot/jquery.colorhelpers.js")
            );

            bundles.Add(new StyleBundle("~/assets/css/master")
                .Include("~/Content/font-awesome/css/font-awesome.min.css", new CssRewriteUrlTransform())
                .Include("~/Content/site.css")
                .Include("~/Content/homepage.css")
                .Include("~/Content/topbar.css", new CssRewriteUrlTransform())
                .Include("~/Content/header.css", new CssRewriteUrlTransform())
                .Include("~/Content/jquery.autocomplete.css")
            );

            bundles.Add(new StyleBundle("~/assets/css/query")
                .Include("~/Content/codemirror/codemirror.css")
                .Include("~/Content/codemirror/custom.css")
                .Include("~/Content/codemirror/theme.css")
                .Include("~/Content/slickgrid/slick.grid.css", new CssRewriteUrlTransform())
                .Include("~/Content/slickgrid.css", new CssRewriteUrlTransform())
                .Include("~/Content/qp/qp.css")
            );
        }
    }
}