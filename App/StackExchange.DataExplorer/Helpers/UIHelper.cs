using System.Web;
using System.Web.Mvc;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Helpers
{
    public static class UIHelper
    { 
        public static IHtmlString PageNumber<T>(string href, PagedList<T> list, string cssClass = "pager fl")
        {
            return PageNumber(href, list.PageCount, list.PageIndex, cssClass);
        }
        
        public static IHtmlString PageNumber(this HtmlHelper html, string href, int pageCount, int pageIndex, string cssClass)
        {
            return PageNumber(href, pageCount, pageIndex, cssClass);
        }
        
       
        public static IHtmlString PageNumber(string href, int pageCount, int pageIndex, string cssClass)
        {
            return PageNumber(href.ToLower(), pageCount, pageIndex, cssClass, "");
        }

        public static IHtmlString PageNumber(string href, int pageCount, int pageIndex, string cssClass, string urlAnchor)
        {
            href += urlAnchor;
            PageNumber nav = new PageNumber(href.ToLower(), pageCount, pageIndex, cssClass);
            return MvcHtmlString.Create(nav.ToString());
        }

        private static Regex _removePage = new Regex(@"page=\d+&amp;", RegexOptions.Compiled);
        public static IHtmlString PageSizer(string href, int pageIndex, int currentPageSize, int pageCount, string cssClass)
        {
            href = _removePage.Replace(href, "");
            PageSizer sizer = new PageSizer(href.ToLower(), pageIndex, currentPageSize, pageCount, cssClass);
            return MvcHtmlString.Create(sizer.ToString());
        }
    }
}