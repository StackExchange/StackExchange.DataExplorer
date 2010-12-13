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
    using System.Web.Mail;
    using NameValueCollection = System.Collections.Specialized.NameValueCollection;
    using Comparer = System.Collections.Comparer;
    using StringWriter = System.IO.StringWriter;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Renders an HTML page displaying details about an error from the error log.
    /// </summary>
    internal sealed class ErrorDetailPage : ErrorPageBase
    {
        private ErrorLogEntry _errorEntry;

        protected override void OnLoad(EventArgs e)
        {
            string errorId = this.Request.QueryString["id"] ?? "";
            if (errorId.Length == 0) return;

            _errorEntry = this.ErrorLog.GetError(errorId);
            if (_errorEntry == null) return;

            this.Title = string.Format("Error: {0} [{1}]", _errorEntry.Error.Type, _errorEntry.Id);
            base.OnLoad(e);
        }

        protected override void RenderHead(HtmlTextWriter w)
        {
            base.RenderHead(w);
        }

        protected override void RenderContents(HtmlTextWriter w)
        {
            if (_errorEntry != null)
            {
                RenderError(w);
            }
            else
            {
                RenderNoError(w);
            }
        }

        private void RenderNoError(HtmlTextWriter w)
        {
            w.RenderBeginTag(HtmlTextWriterTag.P);
            w.Write("Error not found in log.");
            w.RenderEndTag(); // </p>
            w.WriteLine();
        }



        private void RenderError(HtmlTextWriter w)
        {
            Error error = _errorEntry.Error;
            
            if (error.WebHostHtmlMessage.Length != 0)
            {
                w.Write(@"<div id=""error-view"">");
                string htmlUrl = this.BasePageName + "/html?id=" + _errorEntry.Id;
                w.Write(String.Format(@"<a href=""{0}"" title="""">view original error message (as seen by user)</a>", htmlUrl));
                w.Write("</div>");
            }
            
            w.AddAttribute(HtmlTextWriterAttribute.Id, "PageTitle");
            w.RenderBeginTag(HtmlTextWriterTag.P);
            Server.HtmlEncode(error.Message, w);
            w.RenderEndTag(); // </p>
            w.WriteLine();

            w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorTitle");
            w.RenderBeginTag(HtmlTextWriterTag.P);

            w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorType");
            w.RenderBeginTag(HtmlTextWriterTag.Span);
            Server.HtmlEncode(error.Type, w);
            w.RenderEndTag(); // </span>

            w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorTypeMessageSeparator");
            w.RenderBeginTag(HtmlTextWriterTag.Span);
            w.Write(": ");
            w.RenderEndTag(); // </span>

            //w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorMessage");
            //w.RenderBeginTag(HtmlTextWriterTag.Span);
            //Server.HtmlEncode(error.Message, w);
            //w.RenderEndTag(); // </span>
            w.RenderEndTag(); // </p>
            w.WriteLine();

            // Do we have details, like the stack trace? If so, then write 
            // them out in a pre-formatted (pre) element. 
            if (error.Detail.Length != 0)
            {
                w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorDetail");
                w.RenderBeginTag(HtmlTextWriterTag.Pre);
                w.Flush();
                Server.HtmlEncode(error.Detail, w.InnerWriter);
                w.RenderEndTag(); // </pre>
                w.WriteLine();
            }

            // Write out the error log time, in local server time
            w.AddAttribute(HtmlTextWriterAttribute.Id, "ErrorLogTime");
            w.RenderBeginTag(HtmlTextWriterTag.P);
            w.Write(string.Format(@"occurred <b>{2}</b> on {0} at {1}",
                error.Time.ToLongDateString(),
                error.Time.ToLongTimeString(), error.Time.ToRelativeTime()), w);
            w.RenderEndTag(); // </p>
            w.WriteLine();


            // If this error has context, then write it out.
            RenderCollection(w, error.ServerVariables, "ServerVariables", "Server Variables");
            RenderCollection(w, error.Form, "Form", "Form");
            RenderCollection(w, error.Cookies, "Cookies", "Cookies");
            RenderCollection(w, error.QueryString, "QueryString", "QueryString");

            base.RenderContents(w);
        }

        private string PrepareCell(string s)
        {
            if (Regex.IsMatch(s, @"%[A-Z0-9][A-Z0-9]"))
            {
                s = Server.UrlDecode(s);
            }

            if (Regex.IsMatch(s, "^(https?|ftp|file)://"))
            {
                return Regex.Replace(s, @"((?:https?|ftp|file)://.*)", @"<a href=""$1"">$1</a>");
            }

            if (Regex.IsMatch(s, "/[^ /,]+/"))
            {
                // block special case of "/LM/W3SVC/1"
                if (!s.Contains("/LM"))
                {
                    return Regex.Replace(s, @"(.*)", @"<a href=""$1"">$1</a>");
                }
            }

            return Server.HtmlEncode(s);
        }

        private string _hidden_keys = "|ALL_HTTP|ALL_RAW|HTTP_COOKIE|HTTP_CONTENT_LENGTH|HTTP_CONTENT_TYPE|QUERY_STRING|";
        private string _unimportant_keys = "|HTTP_ACCEPT_ENCODING|HTTP_ACCEPT_LANGUAGE|HTTP_CONNECTION|HTTP_HOST|HTTP_KEEP_ALIVE|PATH_TRANSLATED|SERVER_NAME|SERVER_PORT|SERVER_PORT_SECURE|SERVER_PROTOCOL|HTTP_ACCEPT|HTTP_ACCEPT_CHARSET|APPL_PHYSICAL_PATH|GATEWAY_INTERFACE|HTTPS|INSTANCE_ID|INSTANCE_META_PATH|SERVER_SOFTWARE|APPL_MD_PATH|PATH_INFO|SCRIPT_NAME|REMOTE_PORT|";

        private void RenderCollection(HtmlTextWriter w, NameValueCollection c, string id, string title)
        {
            if (c == null || c.Count == 0) return;

            w.AddAttribute(HtmlTextWriterAttribute.Id, id);
            w.RenderBeginTag(HtmlTextWriterTag.Div);

            w.AddAttribute(HtmlTextWriterAttribute.Class, "table-caption");
            w.RenderBeginTag(HtmlTextWriterTag.P);
            this.Server.HtmlEncode(title, w);
            w.RenderEndTag(); // </p>
            w.WriteLine();

            w.AddAttribute(HtmlTextWriterAttribute.Class, "scroll-view");
            w.RenderBeginTag(HtmlTextWriterTag.Div);

            Table table = new Table();
            table.CellSpacing = 0;

            string[] keys = c.AllKeys;
            Array.Sort(keys, Comparer.DefaultInvariant);
            int i = 0;

            foreach (var key in keys)
            {
                string value = c[key];

                if (!String.IsNullOrEmpty(value) && !IsHidden(key))
                {
                    
                    bool unimportant = IsUnimportant(key);
                    string matchingKeys = "";

                    if (!unimportant)
                    {
                        matchingKeys = GetMatchingKeys(c, value);
                        if (matchingKeys != null)
                        {
                            _hidden_keys += matchingKeys.Replace(", ", "|") + "|";
                        }
                    }
                    
                    TableRow bodyRow = new TableRow();

                    if (unimportant)
                        bodyRow.CssClass = "unimportant-row";
                    else
                    {
                        i++;
                        bodyRow.CssClass = i % 2 == 0 ? "even-row" : "odd-row";
                    }

                    TableCell cell;

                    // key
                    cell = new TableCell();
                    if (!String.IsNullOrEmpty(matchingKeys))
                        cell.Text = Server.HtmlEncode(matchingKeys);
                    else
                        cell.Text = Server.HtmlEncode(key);
                    cell.CssClass = "key-col";
                    bodyRow.Cells.Add(cell);

                    // value                    
                    cell = new TableCell();
                    cell.Text = PrepareCell(value);
                    cell.CssClass = "value-col";
                    bodyRow.Cells.Add(cell);

                    table.Rows.Add(bodyRow);
                }
                
            }

            table.RenderControl(w);

            w.RenderEndTag(); // </div>
            w.WriteLine();
            w.RenderEndTag(); // </div>
            w.WriteLine();
        }

        /// <summary>
        /// returns true of the target is contained in the list;
        /// presumes list is pipe delimited like |apples|oranges|pears|
        /// </summary>
        private bool Matches(string list, string target)
        {
            return list.Contains("|" + target + "|");
        }

        private bool IsUnimportant(string key)
        {
            return Matches(_unimportant_keys, key);
        }

        private bool IsHidden(string key)
        {
            return Matches(_hidden_keys, key);
        }

        private string GetMatchingKeys(NameValueCollection nvc, string s)
        {
            string matchingKeys = "";
            int matches = 0;
            foreach (string key in nvc.Keys)
            {
                if (nvc[key] == s && !IsUnimportant(key) && !IsHidden(key))
                {
                    matches++;
                    matchingKeys += key + ", ";
                }
            }
            if (matches == 1)
                return null;
            else
                return matchingKeys.Substring(0, matchingKeys.Length - 2);
        }
    }
}