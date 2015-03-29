using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class ErrorController : StackOverflowController
    {
        [StackRoute("error")]
        public ActionResult ErrorPage()
        {
            return View("Error");
        }

        [StackRoute("404")]
        public new ActionResult PageNotFound()
        {
            return base.PageNotFound();
        }
    }
}