using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    public class AdminController : StackOverflowController
    {
        [Route("admin")]
        public ActionResult Index()
        {
            if (!Allowed())
            {
                return TextPlainNotFound();
            }

            return View();
        }

        [Route("admin/refresh_stats", HttpVerbs.Post)]
        public ActionResult RefreshStats()
        {
            if (!Allowed())
            {
                return TextPlainNotFound();
            }

            foreach (Site site in Current.DB.Sites)
            {
                site.UpdateStats();
            }

            return Content("sucess");
        }

        public bool Allowed()
        {
            return CurrentUser.IsAdmin;
        }
    }
}