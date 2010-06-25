using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class ErrorController : StackOverflowController
    {
        [Route("error")]
        public ActionResult ErrorPage()
        {
            return View("Error");
        }

        [Route("404")]
        public new ActionResult PageNotFound()
        {
            return base.PageNotFound();
        }
    }
}