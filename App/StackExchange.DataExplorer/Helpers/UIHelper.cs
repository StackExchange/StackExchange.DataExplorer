using System.Web;
using System.Web.Mvc;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Helpers
{
    public static class UIHelper
    { 
        public static IHtmlString PageNumber<T>(string href, PagedList<T> list, string cssClass = "pager fl") => 
            PageNumber(href, list.PageCount, list.PageIndex, cssClass);
        
        public static IHtmlString PageNumber(string href, int pageCount, int pageIndex, string cssClass) => 
            PageNumber(href.ToLower(), pageCount, pageIndex, cssClass, "");

        public static IHtmlString PageNumber(string href, int pageCount, int pageIndex, string cssClass, string urlAnchor)
        {
            href += urlAnchor;
            var nav = new PageNumber(href.ToLower(), pageCount, pageIndex, cssClass);
            return MvcHtmlString.Create(nav.ToString());
        }

        private static readonly Regex _removePage = new Regex(@"page=\d+&amp;", RegexOptions.Compiled);
        public static IHtmlString PageSizer(string href, int pageIndex, int currentPageSize, int pageCount, string cssClass)
        {
            href = _removePage.Replace(href, "");
            var sizer = new PageSizer(href.ToLower(), pageIndex, currentPageSize, pageCount, cssClass);
            return MvcHtmlString.Create(sizer.ToString());
        }
    }
}