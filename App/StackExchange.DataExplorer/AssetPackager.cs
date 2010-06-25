using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace StackExchange.DataExplorer
{
    /// <summary>
    /// This class handles all CSS and Javascript includes 
    /// </summary>
    public static class AssetPackager
    {
        private static readonly Dictionary<string, AssetCollection> cssAssets;

        private static readonly Dictionary<string, AssetCollection> jsAssets;
        private static volatile string versionString;

        static AssetPackager()
        {
            cssAssets =
                new Dictionary<string, AssetCollection>
                    {
                        {"sitecss", new AssetCollection {"/Content/site.css"}},
                        {
                            "viewer_editor", new AssetCollection
                                                 {
                                                     "/Content/smoothness/jquery-ui-1.8.1.custom.css",
                                                     "/Content/slickgrid/slick.grid.css",
                                                     "/Content/codemirror/sqlcolors.css"
                                                 }
                            }
                    };


            jsAssets =
                new Dictionary<string, AssetCollection>
                    {
                        {
                            "viewer", new AssetCollection
                                          {
                                              "/Scripts/codemirror/stringstream.js",
                                              "/Scripts/codemirror/tokenize.js",
                                              "/Scripts/codemirror/highlight.js",
                                              "/Scripts/codemirror/parsesql.js",
                                              "/Scripts/query.js",
                                              "/Scripts/jquery.rule.js",
                                              "/Scripts/jquery.event.drag-1.5.js",
                                              "/Scripts/slick.grid.js",
                                          }
                            },
                        {
                            "editor", new AssetCollection
                                          {
                                              "/Scripts/jquery.rule.js",
                                              "/Scripts/jquery.textarearesizer.js",
                                              "/Scripts/jquery.event.drag-1.5.js",
                                              "/Scripts/slick.grid.js",
                                              "/Scripts/codemirror/codemirror.js",
                                              "/Scripts/query.js"
                                          }
                            }
                    };

            jsAssets.Add("jquery", new AssetCollection("http://ajax.microsoft.com/ajax/jquery/jquery-1.4.2.min.js")
                                       {
                                           "/Scripts/jquery-1.4.2.js"
                                       });

            jsAssets.Add("jquery.validate",
                         new AssetCollection(
                             "http://ajax.microsoft.com/ajax/jquery.validate/1.7/jquery.validate.pack.js")
                             {
                                 "/Scripts/jquery.validate.js"
                             });
        }

        public static string VersionString
        {
            get
            {
                if (versionString == null)
                {
                    versionString = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                return versionString;
            }
        }

        private static string GetInclude(string asset, Dictionary<string, AssetCollection> allAssets, string format,
                                         string extension)
        {
            var buffer = new StringBuilder();

#if DEBUG

            foreach (string include in allAssets[asset])
            {
                buffer.AppendFormat(format,
                                    include, "?v=" + VersionString);
            }
#else
            if (allAssets[asset].ReleaseOverride != null) {
                buffer.AppendFormat(format,
                  allAssets[asset].ReleaseOverride, "");
            } else {
                buffer.AppendFormat(format, "/Content/packaged/" + asset + "." + extension, "?v=" + VersionString);
            }
#endif

            return buffer.ToString();
        }

        public static string ScriptSrc(string asset)
        {
            return GetInclude(asset, jsAssets, @"<script src=""{0}{1}"" type=""text/javascript""></script>", "js");
        }

        public static string LinkCss(string asset)
        {
            return GetInclude(asset, cssAssets, @"<link href=""{0}{1}"" rel=""stylesheet"" type=""text/css"" />", "css");
        }


        /// <summary>
        /// Packages up all our assets 
        /// </summary>
        public static void PackIt(string rootPath)
        {
            PackAssets(rootPath, cssAssets, "css");
            PackAssets(rootPath, jsAssets, "js");
        }

        private static void PackAssets(string rootPath, Dictionary<string, AssetCollection> assets, string extension)
        {
            foreach (var asset in assets)
            {
                var buffer = new StringBuilder();
                if (asset.Value.ReleaseOverride != null)
                {
                    continue;
                }

                foreach (string relativePath in asset.Value)
                {
                    string ResolvedRelativePath = relativePath.Replace('/', '\\').Substring(1);
                    string path = Path.Combine(rootPath, ResolvedRelativePath);
                    buffer.AppendLine(File.ReadAllText(path));
                }

                string target = Path.Combine(rootPath, "Content\\packaged\\" + asset.Key + "." + extension);
                File.WriteAllText(target, buffer.ToString());

                target = Path.GetFullPath(target);

                var psi = new ProcessStartInfo();
                psi.FileName = @"c:\windows\system32\java";
                psi.WorkingDirectory = Path.GetFullPath(Path.Combine(rootPath, @"..\..\Lib"));
                psi.Arguments = string.Format("-jar yuicompressor-2.4.2.jar {0} -o {1} ", target, target);
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process p = Process.Start(psi);
                p.WaitForExit();
            }
        }

        #region Nested type: AssetCollection

        private class AssetCollection : List<string>
        {
            public AssetCollection()
                : this(null)
            {
            }

            public AssetCollection(string releaseOverride)
            {
                ReleaseOverride = releaseOverride;
            }

            public string ReleaseOverride { get; private set; }
        }

        #endregion
    }
}