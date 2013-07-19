using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text;
using StackExchange.AssetPackager;

namespace StackExchange.DataExplorer
{
    /// <summary>
    /// This class handles all CSS and Javascript includes 
    /// </summary>
    public class AssetPackager : Packager<AssetPackager>
    {
        protected override bool CompressAssets
        {
            get
            {
#if DEBUG 
                return false;
#else
                return true;
#endif
            }
        }

        protected override Dictionary<string, AssetCollection> CssAssets
        {
            get
            {

                return new Dictionary<string, AssetCollection>
                {
                    {
                        "sitecss", new AssetCollection
                        {
                            "/Content/font-awesome/css/font-awesome.min.css",
                            "/Content/site.css",
                            "/Content/jquery.autocomplete.css"
                        }
                    },
                    {
                        "query", new AssetCollection
                        {
                            "/Content/smoothness/jquery-ui-1.8.1.custom.css",
                            "/Content/codemirror/codemirror.css",
                            "/Content/codemirror/custom.css",
                            "/Content/codemirror/theme.css",
                            "/Content/slickgrid/slick.grid.css",
                            "/Content/qp/qp.css",
                        }
                    }
                };
            }
        }


        protected override Dictionary<string, AssetCollection> JsAssets
        {
            get
            {
                return new Dictionary<string, AssetCollection>
                {
                    {
                        "flot", new AssetCollection
                        {
                            "/Scripts/flot/jquery.flot.js"
                        }
                    },
                    {
                        "master", new AssetCollection
                        {
                            "/Scripts/master.js",
                            "/Scripts/jquery.autocomplete.js"
                        }
                    },
                    {
                        "query", new AssetCollection
                        {
                            "/Scripts/date.js",
                            "/Scripts/jquery.textarearesizer.js",
                            "/Scripts/jquery.event.drag-2.0.js",
                            "/Scripts/slick.core.js",
                            "/Scripts/slick.grid.js",
                            "/Scripts/codemirror/codemirror.js",
                            "/Scripts/codemirror/sql.js",
                            "/Scripts/codemirror/runmode.js",
                            "/Scripts/query.js",
                            "/Scripts/qp.js",
                            "/Scripts/query.siteswitcher.js",
                            "/Scripts/query.tablehelpers.js"
                        }
                    },
                    {
                        "jquery", new AssetCollection("//ajax.googleapis.com/ajax/libs/jquery/1.7.1/jquery.min.js")
                        {
                            "/Scripts/jquery-1.7.1.js"
                        }
                    }
                };
            }
        }
    }
}