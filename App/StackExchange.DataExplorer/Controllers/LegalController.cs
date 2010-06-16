using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.ViewModel;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Controllers
{
    public class LegalController : StackOverflowController
    {
        [Route("legal")]
        public ActionResult TermsOfService() {
            SetHeaderData("terms of service");
            return View();
        }

        [Route("legal/privacy-policy")]
        public ActionResult PrivacyPolicy() {
            SetHeaderData("privacy policy");
            return View();
        }

        [Route("legal/content-policy")]
        public ActionResult ContentPolicy() {
            SetHeaderData("content policy");
            return View();
        }

        [Route("legal/trademark-guidance")]
        public ActionResult TrademarkGuidance() {
            SetHeaderData("trademark guidance");
            return View();
        }

        private void SetHeaderData(string current) {
            SetHeader("Legal",
               new SubHeaderViewData()
               {
                   Description = "terms of service",
                   Title = "terms of service",
                   Href = "/legal",
                   Selected = (current == "terms of service")
               },
               new SubHeaderViewData()
               {
                   Description = "privacy policy",
                   Title = "privacy policy",
                   Href = "/legal/privacy-policy",
                   Selected = (current == "privacy policy")
               },
               new SubHeaderViewData()
               {
                   Description = "content policy",
                   Title = "content policy",
                   Href = "/legal/content-policy",
                   Selected = (current == "content policy")
               },
               new SubHeaderViewData()
               {
                   Description = "trademark guidance",
                   Title = "trademark guidance",
                   Href = "/legal/trademark-guidance",
                   Selected = (current == "trademark guidance")
               }

               );
        
        }
    }
}
