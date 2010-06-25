using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    [HandleError]
    public class HomeController : StackOverflowController
    {
        [Route("")]
        public ActionResult Index()
        {
            SetHeader("Choose a Site");
            SelectMenuItem("Home");

            return View(Current.DB.Sites.ToList());
        }

        [Route("about")]
        public ActionResult About()
        {
            SetHeader("About");

            return View();
        }


        [Route("faq")]
        public ActionResult Faq()
        {
            SetHeader("Frequently Asked Questions");

            return View();
        }
    }
}