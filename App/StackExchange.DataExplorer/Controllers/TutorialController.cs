using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class TutorialController : StackOverflowController
    {
        [StackRoute("tutorial")]
        public ActionResult Index()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Tutorial - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/intro-to-databases")]
        public ActionResult DatabasePrimer()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Introduction to Databases - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/intro-to-queries")]
        public ActionResult Queries()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Introduction to Queries - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/query-basics")]
        public ActionResult QueryBasics()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Query Basics - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/query-joins")]
        public ActionResult QueryJoins()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Query Joins - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/query-parameters")]
        public ActionResult QueryParameters()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Query Parameters - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/query-computations")]
        public ActionResult QueryComputations()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Query Computations - Stack Exchange Data Explorer";
            return View();
        }

        [StackRoute("tutorial/next-steps")]
        public ActionResult NextSteps()
        {
            SetHeader("Tutorial");
            ViewData["PageTitle"] = "Next Steps - Stack Exchange Data Explorer";
            return View();
        }
    }
}
