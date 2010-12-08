using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace StackExchange.AssetPackager
{

    /// <summary>
    /// This class handles all CSS and Javascript includes 
    /// </summary>
    public abstract class Packager<T> where T : Packager<T>, new()
    {
        private static T Instance = new T();
 
        protected abstract Dictionary<string, AssetCollection> CssAssets { get; }
        protected abstract Dictionary<string, AssetCollection> JsAssets { get; }
        protected abstract bool CompressAssets { get; }

        Dictionary<string, AssetCollection> _cssAssets;
        // memoize
        private Dictionary<string, AssetCollection> cssAssets
        {
            get
            {
                if (_cssAssets == null)
                {
                    _cssAssets = CssAssets;
                }
                return _cssAssets;
            }
        }

        Dictionary<string, AssetCollection> _jsAssets;
        // memoize
        private Dictionary<string, AssetCollection> jsAssets
        {
            get
            {
                if (_jsAssets == null)
                {
                    _jsAssets = JsAssets;
                }
                return _jsAssets;
            }
        }

        private static volatile string versionString;

        public string VersionString
        {
            get
            {
                if (versionString == null)
                {
                    versionString = this.GetType().Assembly.GetName().Version.ToString();
                }
                return versionString;
            }
        }

        private string GetInclude(string asset, Dictionary<string, AssetCollection> allAssets, string format,
                                         string extension)
        {
            var buffer = new StringBuilder();

            if (!CompressAssets)
            {

                foreach (string include in allAssets[asset])
                {
                    buffer.AppendFormat(format,
                                        include, "?v=" + VersionString);
                }
            }
            else
            {
                if (allAssets[asset].ReleaseOverride != null)
                {
                    buffer.AppendFormat(format,
                      allAssets[asset].ReleaseOverride, "");
                }
                else
                {
                    buffer.AppendFormat(format, "/Content/packaged/" + asset + "." + extension, "?v=" + VersionString);
                }
            }

            return buffer.ToString();
        }

        public static string ScriptSrc(string asset)
        {
            return Instance.GetInclude(asset, Instance.jsAssets, @"<script src=""{0}{1}"" type=""text/javascript""></script>", "js");
        }

        public static string LinkCss(string asset) 
        {
            return Instance.GetInclude(asset, Instance.cssAssets, @"<link href=""{0}{1}"" rel=""stylesheet"" type=""text/css"" />", "css");
        }


        /// <summary>
        /// Packages up all our assets 
        /// </summary>
        public static void PackIt(string rootPath)
        {
            PackAssets(rootPath, Instance.cssAssets, "css");
            PackAssets(rootPath, Instance.jsAssets, "js");
        }

        private static void PackAssets(string rootPath, Dictionary<string, AssetCollection> assets, string extension)
        {
            var packagedDir = Path.Combine(rootPath, "Content\\packaged");
            if (!Directory.Exists(packagedDir))
            {
                Directory.CreateDirectory(packagedDir);
            }

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

                string target = Path.Combine(packagedDir, asset.Key + "." + extension);

                string compressed;
                if (extension.ToLower() == "js")
                {
                    compressed = Yahoo.Yui.Compressor.JavaScriptCompressor.Compress(buffer.ToString());
                }
                else
                {
                    compressed = Yahoo.Yui.Compressor.YUICompressor.Compress(buffer.ToString(), 200);
                }

                File.WriteAllText(target, compressed);
            }
        }

        #region Nested type: AssetCollection

        public class AssetCollection : List<string>
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
