using System;
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

            ViewData["LastUpdate"] = Current.DB.Query<DateTime?>("SELECT MAX(LastPost) FROM Sites").FirstOrDefault();

            return View(sites);
        }

        [Route("help")]
        public ActionResult Help()
        {
            SetHeader("Using Data Explorer");

            var version = Current.DB.Query<string>("SELECT @@VERSION").FirstOrDefault();

            if (version != null)
            {
                version = string.Join(" ", version.Split(new char[] { ' ' }).Skip(1).Take(3));
            }

            ViewData["LastUpdate"] = Current.DB.Query<DateTime?>("SELECT MAX(LastPost) FROM Sites").FirstOrDefault();
            ViewData["DbVersion"] = version;
            ViewData["AspVersion"] = typeof(Controller).Assembly.GetName().Version.ToString(2);

            return View();
        }

        [Route("about")]
        public ActionResult About()
        {
            return Redirect("/help");
        }


        [Route("faq")]
        public ActionResult Faq()
        {
            return Redirect("/help");
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
                        IconUrl = site.IconUrl,
                        LongName = site.LongName
                    };
                }
            );

            return Json(sites);
        }

        [Route("legal/{subpath?}")]
        public ActionResult Legal(string subpath)
        {
            return RedirectPermanent("http://stackexchange.com/legal" + (subpath.HasValue() ? "/" + subpath : ""));
        }
    }
}