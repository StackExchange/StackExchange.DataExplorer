namespace SimpleErrorHandler
{
    using System;
    using System.Web.UI;

    /// <summary>
    /// Renders an HTML page displaying the detailed host-generated (ASP.NET)
    /// HTML recorded for an error from the error log.
    /// </summary>
    internal sealed class ErrorHtmlPage : ErrorPageBase
    {
        protected override void Render(HtmlTextWriter w)
        {
            string errorId = this.Request.QueryString["id"] ?? "";
            if (errorId.Length == 0) return;

            ErrorLogEntry errorEntry = this.ErrorLog.GetError(errorId);
            if (errorEntry == null) return;

            // If we have a host (ASP.NET) formatted HTML message for the error then just stream it out as our response.
            if (errorEntry.Error.WebHostHtmlMessage.Length != 0)
            {
                w.Write(errorEntry.Error.WebHostHtmlMessage);
            }
        }
    }
}
