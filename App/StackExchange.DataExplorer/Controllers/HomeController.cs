using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Models;
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

        [Route("sites")]
        public ActionResult SearchSites()
        {
            var sites = Current.DB.Query<Site>("SELECT * FROM Sites").Select<Site, object>(
                site =>
                {
                    return new
                    {
                        Id = site.Id,
                        Url = site.Url,
                        Name = site.Name,
                        IconUrl = site.IconProxyUrl,
                        LongName = site.LongName
                    };
                }
            );

            return Json(sites);
        }
    }
}