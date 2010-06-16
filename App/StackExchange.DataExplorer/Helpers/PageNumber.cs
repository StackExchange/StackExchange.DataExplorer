using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;
using System.Text;

namespace StackExchange.DataExplorer.Helpers
{
    public class PageNumber
    {
        public string HRef { get; set; }
        public string CssClass { get; set; }
        public string DivId { get; set; }
        public int PageCount { get; set; }
        public int PageCurrent  { get; set; }
        public bool IsJQuery { get; set; }

        private const int _cellcount = 6;
        private const string _prev = "prev ";
        private const string _next = " next";
        const string PAGER_DOTS = "&hellip;";

        public PageNumber(string href, int pageCount, int pageCurrent, string cssClass)
        {
            HRef = href;
            CssClass = cssClass;
            PageCount = pageCount;
            PageCurrent = pageCurrent;
        }
        public PageNumber(string href, int pageCount, int pageCurrent, string cssClass, string divId, bool isAjax)
        {
            HRef = href;
            CssClass = cssClass;
            DivId = divId; 
            PageCount = pageCount;
            PageCurrent = pageCurrent;
            IsJQuery = isAjax;
        }

        public override string ToString()
        {
            if (PageCount <= 1) return "";

            int curPage = PageCurrent + 1;
            var pages = new List<string>();

            // handle simplest case first: only a few pages!
            if (PageCount <= _cellcount)
            {
                for (int i = 1; i <= PageCount; i++)
                    pages.Add(i.ToString());
            }
            else
            {
                if (curPage < _cellcount - 1)
                {
                    // we're near the start
                    for (int i = 1; i < _cellcount; i++)
                        pages.Add(i.ToString());
                    pages.Add(PAGER_DOTS);
                    pages.Add(PageCount.ToString());
                }                
                else if (curPage > PageCount - _cellcount + 2)
                {
                    // we're near the end
                    pages.Add("1");
                    pages.Add(PAGER_DOTS);
                    for (int i = PageCount - _cellcount + 2; i <= PageCount; i++)
                        pages.Add(i.ToString());
                }
                else
                {
                    // we're in the middle, somewhere
                    pages.Add("1");
                    pages.Add(PAGER_DOTS);
                    int range = _cellcount - 4;
                    for (int i = curPage - range; i <= curPage + range; i++)
                        pages.Add(i.ToString());
                    pages.Add(PAGER_DOTS);
                    pages.Add(PageCount.ToString());
                }
            }

            var sb = new StringBuilder(1024);

            if (CssClass.HasValue() || DivId.HasValue())
            {
                sb.Append("<div ");
                if (DivId.HasValue())
                {
                    sb.Append("id=\"");
                    sb.Append(DivId);
                    sb.Append("\" ");
                }
                if (CssClass.HasValue())
                {
                    sb.Append("class=\"");
                    sb.Append(CssClass);
                    sb.Append("\" ");
                }
                sb.AppendLine(">");
            }            

            if (curPage > 1)
                WriteCell(sb, _prev, "page-numbers");
            foreach (string page in pages)
                WriteCell(sb, page, "page-numbers");
            if (curPage < PageCount)
                WriteCell(sb, _next, "page-numbers");

            if (CssClass.HasValue() || DivId.HasValue())
                sb.AppendLine("</div>");

            return sb.ToString();
        } 
        
        private void WriteCell(StringBuilder sb, string pageText, string cssClass)
        {
            string linktext = pageText;
            string rel = null;
            bool createLink = true;

            if (pageText == PAGER_DOTS)
            {
                cssClass += " dots";
                createLink = false;
            }
            else if (pageText == _prev)
            {
                cssClass += " prev";
                rel = "prev";
                pageText = PageCurrent.ToString();
            }
            else if (pageText == _next)
            {
                cssClass += " next";
                rel = "next";
                pageText = (PageCurrent + 2).ToString();
            }
            else if (pageText == (PageCurrent + 1).ToString())
            {
                cssClass += " current";
                createLink = false;
            }

            if (createLink)
            {
                sb.Append(@"<a href=""");
                sb.Append(HRef.Replace("page=-1", "page=" + pageText));
                sb.Append(@""" title=""go to page ");
                sb.Append(pageText);
                sb.Append(@"""");
                if (rel != null)
                    sb.Append(@" rel=""" + rel + @"""");
                sb.Append(">");
            }
            sb.Append(@"<span class=""");
            sb.Append(cssClass);
            sb.Append(@""">");
            sb.Append(linktext);
            sb.Append("</span>");
            if (createLink)
            {
                sb.Append("</a>");
            }
            sb.AppendLine();
        }
    }
}