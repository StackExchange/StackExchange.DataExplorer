using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using System.IO;

namespace StackExchange.DataExplorer.Controllers
{
    public class ErrorController : StackOverflowController {

        [Route("error")]
        public ActionResult ErrorPage() {
            return View("Error");
        }

        [Route("404")]
        public new ActionResult PageNotFound() {
            return base.PageNotFound();
        }

    }
}
