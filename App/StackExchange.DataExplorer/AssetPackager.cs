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

                return
                new Dictionary<string, AssetCollection>
                    {
                        {"sitecss", new AssetCollection {"/Content/site.css"}},
                        {
                            "viewer_editor", new AssetCollection
                                                 {
                                                     "/Content/smoothness/jquery-ui-1.8.1.custom.css",
                                                     "/Content/slickgrid/slick.grid.css",
                                                     "/Content/codemirror/sqlcolors.css",
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
                return
                new Dictionary<string, AssetCollection>
                    {
                        {
                            "flot", new AssetCollection
                                        {
                                            "/Scripts/flot/jquery.flot.js"
                                        }
                        },
                        {
                            "viewer", new AssetCollection
                                          {
                                              "/Scripts/date.js",
                                              "/Scripts/codemirror/stringstream.js",
                                              "/Scripts/codemirror/tokenize.js",
                                              "/Scripts/codemirror/highlight.js",
                                              "/Scripts/codemirror/parsesql.js",
                                              "/Scripts/query.js",
                                              "/Scripts/jquery.rule.js",
                                              "/Scripts/jquery.event.drag-1.5.js",
                                              "/Scripts/slick.grid.js",
                                              "/Scripts/qp.js",
                                          }
                            },
                        {
                            "editor", new AssetCollection
                                          {
                                              "/Scripts/date.js",
                                              "/Scripts/jquery.rule.js",
                                              "/Scripts/jquery.textarearesizer.js",
                                              "/Scripts/jquery.event.drag-1.5.js",
                                              "/Scripts/slick.grid.js",
                                              "/Scripts/codemirror/codemirror.js",
                                              "/Scripts/query.js",
                                              "/Scripts/qp.js",
                                          }
                            },
                            
                            { "jquery", new AssetCollection("http://ajax.microsoft.com/ajax/jquery/jquery-1.6.4.min.js")
                                       {
                                           "/Scripts/jquery-1.6.4.js"
                                       }
                            },
 
                            {"jquery.validate",
                     new AssetCollection(
                         "http://ajax.microsoft.com/ajax/jquery.validate/1.7/jquery.validate.pack.js")
                             {
                                 "/Scripts/jquery.validate.js"
                             }
                        }
                    
                    };

            }
        }

    }
}