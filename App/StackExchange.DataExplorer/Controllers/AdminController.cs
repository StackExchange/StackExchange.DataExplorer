using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class AdminController : StackOverflowController
    {

        [Route("admin")]
        public ActionResult Index()
        {
            if (!Allowed()) {
                return TextPlainNotFound();
            }

            return View();
        }

        [Route("admin/refresh_stats",HttpVerbs.Post)]
        public ActionResult RefreshStats() {

            if (!Allowed()) {
                return TextPlainNotFound();
            }

            foreach (var site in Current.DB.Sites)
	        {
                site.UpdateStats();
	        } 

            return Content("sucess");         
        }

        public bool Allowed() {
            return CurrentUser.IsAdmin == true;
        }

    }
}
