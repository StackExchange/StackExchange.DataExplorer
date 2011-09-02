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

            var sites = Current.DB.Query<Models.Site>("select * from Sites order by TotalQuestions desc").ToList();

            return View(sites);
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