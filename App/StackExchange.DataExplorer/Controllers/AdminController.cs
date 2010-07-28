using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using System.Linq;

namespace StackExchange.DataExplorer.Controllers
{
    public class AdminController : StackOverflowController
    {

        [Route("admin/whitelist/approve/{id:int}",HttpVerbs.Post)]
        public ActionResult ApproveWhiteListEntry(int id) {
            if (!Allowed())
            {
                return TextPlainNotFound();
            }

            Current.DB.OpenIdWhiteLists.First(w => w.Id == id).Approved = true;
            Current.DB.SubmitChanges();

            return Json("ok");
        }

        [Route("admin/whitelist/remove/{id:int}", HttpVerbs.Post)]
        public ActionResult RemoveWhiteListEntry(int id)
        {
            if (!Allowed())
            {
                return TextPlainNotFound();
            }

            var entry = Current.DB.OpenIdWhiteLists.First(w => w.Id == id);
            Current.DB.OpenIdWhiteLists.DeleteOnSubmit(entry);
            Current.DB.SubmitChanges();

            return Json("ok");
        }

        [Route("admin/whitelist")] 
        public ActionResult WhiteList() {
            if (!Allowed())
            {
                return TextPlainNotFound();
            }

            SetHeader("Open Id Whitelist");

            return View(Current.DB.OpenIdWhiteLists);
        }

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