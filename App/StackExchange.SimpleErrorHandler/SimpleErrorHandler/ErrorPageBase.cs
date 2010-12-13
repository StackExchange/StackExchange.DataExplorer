/*
 This file is derived off ELMAH:

http://code.google.com/p/elmah/

http://www.apache.org/licenses/LICENSE-2.0
 
 */


namespace SimpleErrorHandler
{
    using System;
    using System.Web.UI;
    using System.Text.RegularExpressions;
    using CultureInfo = System.Globalization.CultureInfo;

    /// <summary>
    /// Provides the base implementation and layout for most pages that render HTML for the error log.
    /// </summary>
    internal abstract class ErrorPageBase : Page
    {
        private string _title;

        protected string BasePageName
        {
            get { return this.Request.ServerVariables["URL"]; }
        }

        protected virtual ErrorLog ErrorLog
        {
            get { return ErrorLog.Default; }
        }

        protected new virtual string Title
        {
            get { return _title ?? ""; }
            set { _title = value; }
        }

        protected virtual string ApplicationName
        {
            get { return this.ErrorLog.ApplicationName; }
        }

        protected virtual void RenderDocumentStart(HtmlTextWriter w)
        {            
            w.WriteLine("<html>");
            w.WriteLine("<body>");
            w.WriteLine("<head>");
            RenderHead(w);
            w.WriteLine("</head>");
            w.WriteLine("<body>");
        }

        protected virtual void RenderHead(HtmlTextWriter w)
        {
            w.Write("<title>" + Server.HtmlEncode(this.Title) + "</title>");
            w.WriteLine(@"<link rel=""stylesheet"" type=""text/css"" href=""" + this.BasePageName + @"/stylesheet"" />");
        }

        protected virtual void RenderDocumentEnd(HtmlTextWriter w)
        {
            w.Write(@"<div id=""footer"">");

            // Write out server date, time and time zone details.
            DateTime now = DateTime.Now;

            w.Write(@"<div id=""servertime"">");
            w.Write("Server time is ");
            this.Server.HtmlEncode(now.ToString("G", CultureInfo.InvariantCulture), w);
            w.Write(" ");
            string s = TimeZone.CurrentTimeZone.IsDaylightSavingTime(now) ? 
                TimeZone.CurrentTimeZone.DaylightName : TimeZone.CurrentTimeZone.StandardName;
            foreach (Match m in Regex.Matches(s, @"\b\w"))
            {
                w.Write(m.ToString());
            }
            w.Write(@"</div>");

            // Write the powered-by signature, that includes version information.
            PoweredBy poweredBy = new PoweredBy();
            w.Write(@"<div id=""version"">");
            poweredBy.RenderControl(w);            
            w.Write("; ");
            this.Server.HtmlEncode(this.ErrorLog.Name, w);
            w.Write(@"<div>");

            w.Write(@"</div>"); // footer

            w.WriteLine("</body>");
            w.WriteLine("</html>");
        }

        protected override void Render(HtmlTextWriter w)
        {
            RenderDocumentStart(w);
            RenderContents(w);
            RenderDocumentEnd(w);
        }

        protected virtual void RenderContents(HtmlTextWriter w)
        {
            base.Render(w);
        }
    }
}