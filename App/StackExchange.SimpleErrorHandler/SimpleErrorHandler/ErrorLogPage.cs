namespace SimpleErrorHandler
{
	using System;
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using CultureInfo = System.Globalization.CultureInfo;
    using ArrayList = System.Collections.ArrayList;

    /// <summary>
    /// Renders an HTML page displaying a page of errors from the error log.
    /// </summary>
    internal sealed class ErrorLogPage : ErrorPageBase
	{
        private int _pageIndex;
        private int _pageSize; 
        private int _totalCount;
        private ArrayList _errorEntryList;
        
        private const int _defaultPageSize = 40;
        private const int _maximumPageSize = 100;

        protected override void OnLoad(EventArgs e)
        {
            // Get the page index and size parameters within their bounds.
            _pageSize = Convert.ToInt32(this.Request.QueryString["size"], CultureInfo.InvariantCulture);
            _pageSize = Math.Min(_maximumPageSize, Math.Max(0, _pageSize));

            if (_pageSize == 0)
            {
                _pageSize = _defaultPageSize;
            }

            _pageIndex = Convert.ToInt32(this.Request.QueryString["page"], CultureInfo.InvariantCulture);
            _pageIndex = Math.Max(1, _pageIndex) - 1;

            // Read the error records.
            _errorEntryList = new ArrayList(_pageSize);
            _totalCount = this.ErrorLog.GetErrors(_pageIndex, _pageSize, _errorEntryList);

            // Set the title of the page.
            this.Title = string.Format("Error Log for {0} on {1} (Page {2})", 
                this.ApplicationName, Environment.MachineName, 
                (_pageIndex + 1).ToString());

            base.OnLoad(e);
        }

        protected override void RenderHead(HtmlTextWriter w)
        {
            base.RenderHead(w);

            // Write a <link> tag to relate the RSS feed.
            w.AddAttribute("rel", "alternate");
            w.AddAttribute(HtmlTextWriterAttribute.Type, "application/rss+xml");
            w.AddAttribute(HtmlTextWriterAttribute.Title, "RSS");
            w.AddAttribute(HtmlTextWriterAttribute.Href, this.BasePageName + "/rss");
            w.RenderBeginTag(HtmlTextWriterTag.Link);
            w.RenderEndTag();
            w.WriteLine();

            // If on the first page, then enable auto-refresh every minute
            // by issuing the following markup:
            //
            //      <meta http-equiv="refresh" content="60">
            //
            if (_pageIndex == 0)
            {
                w.AddAttribute("http-equiv", "refresh");
                w.AddAttribute("content", "1200");
                w.RenderBeginTag(HtmlTextWriterTag.Meta);
                w.RenderEndTag();
                w.WriteLine();
            }

        }

        protected override void RenderContents(HtmlTextWriter w)
        {
            // Write out the page title in the body.
            //RenderTitle(w);

            if (_errorEntryList.Count != 0)
            {
                // Write out page navigation links.
                RenderPageNavigators(w);

                w.Write(@"<br clear=""both"">");

                // Write error number range displayed on this page and the
                // total available in the log, followed by stock
                // page sizes.
                w.RenderBeginTag(HtmlTextWriterTag.P);
                
                w.RenderEndTag(); // </p>
                w.WriteLine();

                // Write out the main table to display the errors.
                RenderErrors(w);
            }
            else
            {
                // No errors found in the log, so display a corresponding message.
                RenderNoErrors(w);
            }

            base.RenderContents(w);
        }

	    private void RenderPageNavigators(HtmlTextWriter w)
	    {
	        w.RenderBeginTag(HtmlTextWriterTag.P);

            int firstErrorNumber = _pageIndex * _pageSize + 1;
            int lastErrorNumber = firstErrorNumber + _errorEntryList.Count - 1;
            int totalPages = (int)Math.Ceiling((double)_totalCount / _pageSize);

            ErrorLogEntry errorEntry = (ErrorLogEntry)_errorEntryList[0];

            w.Write(@"<div id=""errorcount"">{0} Errors; last {1}</div>", _totalCount, errorEntry.Error.Time.ToRelativeTime());
    
	        if (_totalCount > _pageSize)
	        {
                w.Write(@"<div id=""pagination"">");
                for (int i = 0; i < (_totalCount / _pageSize) + 1; i++)
                {
                    if (i == _pageIndex)
                    {
                        w.Write(@"<span id=""page-num-current"">");
                    }
                    else
                    {
                        w.Write(@"<span id=""page-num"">");
                    }
                    RenderLinkToPage(w, (i + 1).ToString(), i); 
                    w.Write(@"</span>");
                }
                w.Write(@"</div>");
	        }
	        w.RenderEndTag(); // </p>
            w.WriteLine();
	    }

	    private void RenderTitle(HtmlTextWriter w)
        {
            // If the application name matches the APPL_MD_PATH then its
            // of the form /LM/W3SVC/.../<name>. In this case, use only the 
            // <name> part to reduce the noise. The full application name is 
            // still made available through a tooltip.
            string simpleName = this.ApplicationName;

            if (string.Compare(simpleName, this.Request.ServerVariables["APPL_MD_PATH"], 
                true, CultureInfo.InvariantCulture) == 0)
            {
                int lastSlashIndex = simpleName.LastIndexOf('/');

                if (lastSlashIndex > 0)
                {
                    simpleName = simpleName.Substring(lastSlashIndex + 1);
                }
            }

            w.AddAttribute(HtmlTextWriterAttribute.Id, "PageTitle");
            w.RenderBeginTag(HtmlTextWriterTag.P);

            w.AddAttribute(HtmlTextWriterAttribute.Id, "ApplicationName");
            w.AddAttribute(HtmlTextWriterAttribute.Title, this.Server.HtmlEncode(this.ApplicationName));
            w.RenderBeginTag(HtmlTextWriterTag.Span);
            Server.HtmlEncode(simpleName, w);
            w.Write(" on ");
            Server.HtmlEncode(Environment.MachineName, w);
            w.RenderEndTag(); // </span>

            w.RenderEndTag(); // </p>
            w.WriteLine();
        }

        private void RenderNoErrors(HtmlTextWriter w)
        {
            w.RenderBeginTag(HtmlTextWriterTag.P);
            w.Write(@"<span id=""errorcount"">No errors yet. Yay!</span>");
            w.RenderEndTag();
            w.WriteLine();
        }

        private void RenderErrors(HtmlTextWriter w)
        {
            // Create a table to display error information in each row.
            Table table = new Table();
            table.ID = "ErrorLog";
            table.CellSpacing = 0;
            
            TableRow headRow = new TableRow();

            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "", "type-col")); // actions, e.g. delete, protect
            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "Type", "type-col"));
            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "Error", "error-col"));
            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "Url", "url-col"));
            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "Remote IP", "user-col"));
            headRow.Cells.Add(FormatCell(new TableHeaderCell(), "Time", "user-col"));

            table.Rows.Add(headRow);

            for (int errorIndex = 0; errorIndex < _errorEntryList.Count; errorIndex++)
            {
                ErrorLogEntry errorEntry = (ErrorLogEntry) _errorEntryList[errorIndex];
                Error error = errorEntry.Error;

                TableRow bodyRow = new TableRow();
                bodyRow.CssClass = errorIndex % 2 == 0 ? "even-row" : "odd-row";


                TableCell actionCell = new TableCell();
                bodyRow.Cells.Add(actionCell);

                HyperLink deleteLink = new HyperLink();
                deleteLink.NavigateUrl = this.Request.Path + (Request.Path.EndsWith("/") ? "" : "/") + "delete?id=" + errorEntry.Id;
                deleteLink.CssClass = "delete-link";
                deleteLink.Text = "&nbsp;X&nbsp;";
                deleteLink.ToolTip = "Delete this error";
                actionCell.Controls.Add(deleteLink);

                if (!error.IsProtected)
                {
                    HyperLink protectLink = new HyperLink();
                    protectLink.NavigateUrl = this.Request.Path + (Request.Path.EndsWith("/") ? "" : "/") + "protect?id=" + errorEntry.Id;
                    protectLink.CssClass = "protect-link";
                    protectLink.Text = "&nbsp;P&nbsp;";
                    protectLink.ToolTip = "Protect this error from automatic deletion";
                    actionCell.Controls.Add(protectLink);
                }


                bodyRow.Cells.Add(FormatCell(new TableCell(), GetSimpleErrorType(error), "type-col", error.Type, true));
                    
                TableCell messageCell = new TableCell();
                messageCell.CssClass = "error-col";

                HyperLink detailsLink = new HyperLink();
                detailsLink.NavigateUrl = this.Request.Path + (Request.Path.EndsWith("/") ? "" : "/") + "detail?id=" + errorEntry.Id;
                detailsLink.CssClass = "details-link";
                detailsLink.Text = this.Server.HtmlEncode(error.Message);

                if (error.DuplicateCount.HasValue)
                    detailsLink.Text += string.Format(@" <span class='duplicate-count' title='number of similar errors occurring close to this error'>({0})</span>", error.DuplicateCount);


                messageCell.Controls.Add(detailsLink);
                messageCell.Controls.Add(new LiteralControl(" "));

                bodyRow.Cells.Add(messageCell);

                string url = error.ServerVariables["URL"] ?? "";
                string title = url;
                if (title.Length > 40) title = title.TruncateWithEllipsis(40);

                bodyRow.Cells.Add(FormatCellRaw(new TableCell(), String.Format(@"<a href=""{0}"">{1}</a>", url, title), "user-col"));
                bodyRow.Cells.Add(FormatCell(new TableCell(), GetRemoteIP(error.ServerVariables), "user-col ip-address"));
                
                bodyRow.Cells.Add(FormatCellRaw(new TableCell(), error.Time.ToRelativeTimeSpan(), "user-col"));
                
                table.Rows.Add(bodyRow);
            }

            table.RenderControl(w);
        }

        private TableCell FormatCell(TableCell cell, string contents, string cssClassName)
        {
            return FormatCell(cell, contents, cssClassName, "", true);
        }

        private TableCell FormatCellRaw(TableCell cell, string contents, string cssClassName)
        {
            return FormatCell(cell, contents, cssClassName, "", false);
        }

        private TableCell FormatCell(TableCell cell, string contents, string cssClassName, string toolTip, bool Encode)
        {
            cell.Wrap = false;
            cell.CssClass = cssClassName;

            if (contents.Length == 0)
            {
                cell.Text = "&nbsp;";
                return cell;
            }

            string encodedContents;
            if (Encode)
            {
                encodedContents = this.Server.HtmlEncode(contents);
            }
            else
            {
                encodedContents = contents;
            }
            
            if (toolTip.Length == 0)
            {
                cell.Text = encodedContents;
            }
            else
            {
                Label label = new Label();
                label.ToolTip = toolTip;
                label.Text = encodedContents;
                cell.Controls.Add(label);
            }
            return cell;
        }

        private string GetSimpleErrorType(Error error)
        {
            if (error.Type.Length == 0) return "";

            string simpleType = error.Type;

            int lastDotIndex = CultureInfo.InvariantCulture.CompareInfo.LastIndexOf(simpleType, '.');                
            if (lastDotIndex > 0)
            {
                simpleType = simpleType.Substring(lastDotIndex + 1);
            }

            const string conventionalSuffix = "Exception";

            if (simpleType.Length > conventionalSuffix.Length)
            {
                int suffixIndex = simpleType.Length - conventionalSuffix.Length;
                
                if (string.Compare(simpleType, suffixIndex, conventionalSuffix, 0,
                    conventionalSuffix.Length, true, CultureInfo.InvariantCulture) == 0)
                {
                    simpleType = simpleType.Substring(0, suffixIndex);
                }
            }

            return simpleType;
        }

        private void RenderLinkToPage(HtmlTextWriter w, string text, int pageIndex)
        {
            RenderLinkToPage(w, text, pageIndex, _pageSize);
        }

        private void RenderLinkToPage(HtmlTextWriter w, string text, int pageIndex, int pageSize)
        {
            string href = string.Format("{0}?page={1}&size={2}", 
                this.Request.Path,
                (pageIndex + 1).ToString(CultureInfo.InvariantCulture),
                pageSize.ToString(CultureInfo.InvariantCulture));

            w.AddAttribute(HtmlTextWriterAttribute.Href, href);
            w.RenderBeginTag(HtmlTextWriterTag.A);
            this.Server.HtmlEncode(text, w);
            w.RenderEndTag();
        }


        /// <summary>
        /// When a client IP can't be determined
        /// </summary>
        public const string UnknownIP = "0.0.0.0";

        private static Regex _ipAddress = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string s)
        {
            return (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0."));
        }

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(NameValueCollection ServerVariables)
        {
            var ip = ServerVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = ServerVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue())
            {
                ipForwarded = _ipAddress.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : UnknownIP;
        }
    }
}