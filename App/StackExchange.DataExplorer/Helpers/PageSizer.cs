using System.Text;

namespace StackExchange.DataExplorer.Helpers
{
    public class PageSizer
    {
        private static readonly int[] _pageSizes = {30, 50, 100};

        public PageSizer(string href, int pageIndex, int currentPageSize, int pageCount, string cssClass)
        {
            HRef = href;
            PageIndex = pageIndex;
            CurrentPageSize = currentPageSize;
            PageCount = pageCount;
            CssClass = cssClass;
        }

        public int PageIndex { get; set; }
        public string HRef { get; set; }
        public int CurrentPageSize { get; set; }
        public int PageCount { get; set; }
        public string CssClass { get; set; }

        public static int DefaultPageSize => _pageSizes[0];

        public override string ToString()
        {
            if (PageCount == 1)
                return "";

            var sb = new StringBuilder(512);

            sb.Append(@"<div class=""");
            sb.Append(CssClass);
            sb.Append(@""">");

            foreach (int pageSize in _pageSizes)
            {
                sb.Append(@"<a href=""");
                sb.Append(HRef.Replace("pagesize=-1", "pagesize=" + pageSize));
                sb.Append(@""" title=""");
                sb.Append("show ");
                sb.Append(pageSize);
                sb.Append(@" items per page""");
                if (pageSize == CurrentPageSize)
                    sb.Append(@" class=""current page-numbers""");
                else
                    sb.Append(@" class=""page-numbers""");
                sb.Append(">");
                sb.Append(pageSize);
                sb.AppendLine("</a>");
            }
            sb.AppendLine(@"<span class=""page-numbers desc"">per page</span>");
            sb.Append("</div>");

            return sb.ToString();
        }

        public static int? ValidatePageSize(int? pageSize)
        {
            if (pageSize > 0 && pageSize.Value <= 50) return pageSize;
            return null;
        }
    }
}