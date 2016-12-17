using System;
using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    public class RedirectPermanentResult : ActionResult
    {
        public string Url { get; }

        public RedirectPermanentResult(string url)
        {
            if (url.IsNullOrEmpty())
            {
                throw new ArgumentException("url should not be empty");
            }

            Url = url;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (context.IsChildAction)
            {
                throw new InvalidOperationException("You can not redirect in child actions");
            }

            string destinationUrl = UrlHelper.GenerateContentUrl(Url, context.HttpContext);
            context.Controller.TempData.Keep();
            context.HttpContext.Response.RedirectPermanent(destinationUrl, false /* endResponse */);
        }
    }
}