using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class ErrorController : StackOverflowController
    {
        [StackRoute("error")]
        public ActionResult ErrorPage() => View("Error");

        [StackRoute("404")]
        public new ActionResult PageNotFound() => base.PageNotFound();
    }
}