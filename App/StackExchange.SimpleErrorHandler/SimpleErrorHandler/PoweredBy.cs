/*
 This file is derived off ELMAH:

http://code.google.com/p/elmah/

http://www.apache.org/licenses/LICENSE-2.0
 
 */

namespace SimpleErrorHandler
{
    using System;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using Assembly = System.Reflection.Assembly;
    using HttpUtility = System.Web.HttpUtility;
    using FileVersionInfo = System.Diagnostics.FileVersionInfo;
    using Cache = System.Web.Caching.Cache;
    using CacheItemPriority = System.Web.Caching.CacheItemPriority;
    using HttpRuntime = System.Web.HttpRuntime;
    
    /// <summary>
    /// Displays a "Powered-by" message that also contains the assembly
    /// file version informatin and copyright notice.
    /// </summary>
    public sealed class PoweredBy : WebControl
    {
        private FileVersionInfo _versionInfo;

        /// <summary>
        /// Renders the contents of the control into the specified writer
        /// </summary>
        protected override void RenderContents(HtmlTextWriter w)
        {
            HttpUtility.HtmlEncode(this.VersionInfo.ProductName, w);
            w.Write(" ");
            HttpUtility.HtmlEncode(this.VersionInfo.FileVersion, w);
        }

        public override string ToString()
        {
            return this.VersionInfo.ProductName + " " + this.VersionInfo.FileVersion;
        }

        private FileVersionInfo VersionInfo
        {
            get
            {
                string cacheKey = GetType().Name;

                if (this.Cache != null)
                {
                    _versionInfo = (FileVersionInfo)this.Cache[cacheKey];
                }

                // Not found in the cache? Go out and get the version 
                if (_versionInfo == null)
                {
                    Assembly thisAssembly = this.GetType().Assembly;
                    _versionInfo = FileVersionInfo.GetVersionInfo(thisAssembly.Location);
                    // Cache for next time if the cache is available.
                    if (this.Cache != null)
                    {
                        this.Cache.Add(cacheKey, _versionInfo,
                            null, Cache.NoAbsoluteExpiration,
                            TimeSpan.FromMinutes(2), CacheItemPriority.Normal, null);
                    }
                }

                return _versionInfo;
            }
        }

        private Cache Cache
        {
            get
            {
                if (this.Page != null)
                    return this.Page.Cache;
                return HttpRuntime.Cache;
            }
        }
    }
}