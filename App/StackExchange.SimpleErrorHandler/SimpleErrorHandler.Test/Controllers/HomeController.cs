using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SimpleErrorHandler.Test.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {
        [Route("")]
        [Route("home")]
        public ActionResult Index()
        {
            
            return View();
        }

        [Route("home/errors/{resource}/{subResource}", Optional = new string[] { "resource", "subResource" })]
        public ActionResult InvokeSimpleErrorHandler(string resource, string subResource)
        {
            using (var writer = new StringWriter())
            {
                var response = new HttpResponse(writer);
                var context = new HttpContext(System.Web.HttpContext.Current.Request, response);
                var factory = new SimpleErrorHandler.ErrorLogPageFactory();

                var page = factory.GetHandler(context, Request.RequestType, Request.Url.ToString(), Request.PathInfo);
                page.ProcessRequest(context);

                return Content(writer.ToString());
            }
        }
    }
}
