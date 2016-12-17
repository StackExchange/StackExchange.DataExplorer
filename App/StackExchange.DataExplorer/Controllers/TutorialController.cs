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
            return View();
        }

        [StackRoute("tutorial/intro-to-databases")]
        public ActionResult DatabasePrimer()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/intro-to-queries")]
        public ActionResult Queries()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/query-basics")]
        public ActionResult QueryBasics()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/query-joins")]
        public ActionResult QueryJoins()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/query-parameters")]
        public ActionResult QueryParameters()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/query-computations")]
        public ActionResult QueryComputations()
        {
            SetHeader("Tutorial");
            return View();
        }

        [StackRoute("tutorial/next-steps")]
        public ActionResult NextSteps()
        {
            SetHeader("Tutorial");
            return View();
        }
    }
}