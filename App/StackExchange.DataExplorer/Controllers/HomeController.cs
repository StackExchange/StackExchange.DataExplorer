using System;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    [HandleError]
    public class HomeController : StackOverflowController
    {
        [StackRoute("")]
        public ActionResult Index()
        {
            SetHeader("Choose a Site");
            SelectMenuItem("Home");

            var sites = Current.DB.Query<Site>("select * from Sites order by TotalQuestions desc").ToList();

            ViewData["LastUpdate"] = Current.DB.Query<DateTime?>("SELECT MAX(LastPost) FROM Sites").FirstOrDefault();

            return View(sites);
        }

        [StackRoute("help")]
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

        [StackRoute("about")]
        public ActionResult About() => Redirect("/help");
        
        [StackRoute("faq")]
        public ActionResult Faq() => Redirect("/help");

        [StackRoute("sites")]
        public ActionResult SearchSites()
        {
            var sites = Current.DB.Query<Site>("SELECT * FROM Sites").Select<Site, object>(
                site => new
                {
                    site.Id,
                    site.Url,
                    site.Name,
                    site.IconUrl,
                    site.LongName
                });

            return Json(sites);
        }

        [StackRoute("legal/{subpath?}")]
        public ActionResult Legal(string subpath)
        {
            return RedirectPermanent("http://stackexchange.com/legal" + (subpath.HasValue() ? "/" + subpath : ""));
        }
    }
}