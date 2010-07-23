using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.UI;
using log4net;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;

namespace StackExchange.DataExplorer.Controllers
{
    public class StackOverflowController : Controller
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<SubHeaderViewData> menu = new List<SubHeaderViewData>();

        private Site site;

        public Site Site
        {
            get
            {
                if (site == null)
                {
                    int siteId = -1;
                    Int32.TryParse((Session["SiteId"] ?? "").ToString(), out siteId);
                    site = Current.DB.Sites.FirstOrDefault(s => s.Id == siteId);
                    if (site == null)
                    {
                        site = Current.DB.Sites.First();
                    }
                }
                return site;
            }
            set
            {
                site = value;
                Session["SiteId"] = site.Id;
                foreach (SubHeaderViewData menuItem in menu)
                {
                    if (menuItem.Title == "Queries")
                    {
                        menuItem.Href = "/" + site.Name.ToLower() + "/queries";
                    }
                    if (menuItem.Title == "Compose Query")
                    {
                        menuItem.Href = "/" + site.Name.ToLower() + "/query/new";
                    }
                }
            }
        }

        public Site GetSite(string sitename)
        {
            return Current.DB.Sites.First(s => s.Name.ToLower() == sitename);
        }


#if DEBUG
        private Stopwatch watch;
#endif

        protected override void Initialize(RequestContext requestContext)
        {
#if DEBUG
            watch = new Stopwatch();
            watch.Start();
#endif
           
            Current.Controller = this; // allow code to easily find this controller
            ValidateRequest = false; // allow html/sql in form values - remember to validate!
            base.Initialize(requestContext);

            if (WhiteListEnabled && !(this is AccountController) && CurrentUser.IsAnonymous) {
                requestContext.HttpContext.Response.Redirect("/account/login");
            }


            if (!CurrentUser.IsAnonymous &&
                (
                    CurrentUser.LastSeenDate == null ||
                    (DateTime.UtcNow - CurrentUser.LastSeenDate.Value).TotalSeconds > 120)
                )
            {
                CurrentUser.LastSeenDate = DateTime.UtcNow;
                CurrentUser.IPAddress = GetRemoteIP();
                Current.DB.SubmitChanges();
            }


            AddMenuItem(new SubHeaderViewData
                            {
                                Description = "Home",
                                Href = "/",
                                RightAlign = false,
                                Title = "Home"
                            });


            AddMenuItem(new SubHeaderViewData
                            {
                                Title = "Queries",
                                Description = "Queries",
                                Href = "/" + Site.Name.ToLower() + "/queries"
                            });

            AddMenuItem(new SubHeaderViewData
                            {
                                Description = "Users",
                                Href = "/users",
                                RightAlign = false,
                                Title = "Users"
                            });

            AddMenuItem(new SubHeaderViewData
                            {
                                Title = "Compose Query",
                                Description = "Compose Query",
                                Href = "/" + Site.Name.ToLower() + "/query/new",
                                RightAlign = true
                            });


            ViewData["Menu"] = menu;
        }

        public void AddMenuItem(SubHeaderViewData data)
        {
            AddMenuItem(data, -1);
        }

        public void AddMenuItem(SubHeaderViewData data, int index)
        {
            if (index > 0)
            {
                menu.Insert(index, data);
            }
            else
            {
                menu.Add(data);
            }
        }

        public void SelectMenuItem(string title)
        {
            foreach (SubHeaderViewData item in menu)
            {
                if (item.Title == title)
                {
                    item.Selected = true;
                }
            }
        }

        public void SetHeader(string title)
        {
            SetHeader(title, null);
        }

        public void SetHeader(string title, params SubHeaderViewData[] tabs)
        {
            ViewData["Header"] = new SubHeader(title) {Items = tabs};
        }

        protected CachedResult GetCachedResults(Query query)
        {
            CachedResult cachedResults = null;

            if (query == null) return null;

            DBContext db = Current.DB;

            var p = new ParsedQuery(query.QueryBody, Request.Params);
            if (p.AllParamsSet)
            {
                cachedResults = db.CachedResults
                    .Where(r => r.QueryHash == p.ExecutionHash && r.SiteId == Site.Id)
                    .FirstOrDefault();
            }
            return cachedResults;
        }

        /// <summary>
        /// Gets the shared DataContext to be used by a Request's controllers.
        /// </summary>
        public DBContext DB
        {
            get { return Current.DB; }
        }


        /// <summary>
        /// Indicates that this controller is really a "subcontroller" and does not need shared messages added to the ViewData
        /// </summary>
        public bool IsSubControllerCall { get; set; }

        /// <summary>
        /// called when the url doesn't match any of our known routes
        /// </summary>
        protected override void HandleUnknownAction(string actionName)
        {
            PageNotFound().ExecuteResult(ControllerContext);
        }

#if DEBUG
        /// <summary>
        /// fires after the controller finishes execution
        /// </summary>
        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            log.Debug("OnActionExecuted -> " + Request.Url.PathAndQuery + " Duration: " + watch.ElapsedMilliseconds);
            Trace.WriteLine("OnActionExecuted -> " + Request.Url.PathAndQuery + " Duration: " +
                            watch.ElapsedMilliseconds);

            base.OnActionExecuted(filterContext);
        }
#endif

        /// <summary>
        /// When a client IP can't be determined
        /// </summary>
        public const string UnknownIP = "0.0.0.0";

        private static readonly Regex _ipAddress = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$",
                                                             RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string s)
        {
            return (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0."));
        }

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(NameValueCollection ServerVariables)
        {
            string ip = ServerVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            string ipForwarded = ServerVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue())
            {
                ipForwarded = _ipAddress.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : UnknownIP;
        }

        /// <summary>
        /// Answers the current request's user's ip address; checks for any forwarding proxy
        /// </summary>
        public string GetRemoteIP()
        {
            return GetRemoteIP(Request.ServerVariables);
        }


        private User _currentUser;

        /// <summary>
        /// Gets a User object representing the current request's client.
        /// </summary>
        public User CurrentUser
        {
            get
            {
                if (_currentUser == null) InitCurrentUser();
                return _currentUser;
            }
        }


        /// <summary>
        /// initializes current user based on the current Request's cookies/authentication status. This
        /// method could return a newly created, Anonymous User if no means of identification are found.
        /// </summary>
        protected void InitCurrentUser()
        {
            _currentUser = new User();
            _currentUser.IsAnonymous = true;

            if (Request.IsAuthenticated)
            {
                int id;
                if (Int32.TryParse(User.Identity.Name, out id))
                {
                    User lookup = Current.DB.Users.FirstOrDefault(u => u.Id == id);
                    if (lookup != null)
                    {
                        _currentUser = lookup;
                        _currentUser.IsAnonymous = false;
                    }
                }
                else
                {
                    FormsAuthentication.SignOut();
                }
            }

            _currentUser.IPAddress = Request.UserHostAddress;
        }


        /// <summary>
        /// Answers a view page for the current site, e.g. to view the About-StackOverflow, pass in About for 'viewName'.
        /// </summary>
        protected ViewResult ViewSiteSpecific(string viewName)
        {
            return ViewSiteSpecific(viewName, null);
        }

        /// <summary>
        /// Answers a view page for the current site, e.g. to view the About-StackOverflow, pass in About for 'viewName'.
        /// </summary>
        protected ViewResult ViewSiteSpecific(string viewName, object viewData)
        {
            string name = string.Concat(viewName, "-", "s"); // Current.Site.ResourcesName);
            return viewData == null ? View(name) : View(name, viewData);
        }


        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/plain" as media type.
        /// </summary>
        protected ContentResult TextPlain(object content)
        {
            return new ContentResult {Content = content.ToString(), ContentType = "text/plain"};
        }

        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/html" as media type.
        /// </summary>
        protected ContentResult HtmlRaw(object content)
        {
            return new ContentResult {Content = content.ToString(), ContentType = "text/html"};
        }

        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/html" as media type.
        /// </summary>
        protected ContentResult JsonRaw(object content)
        {
            return new ContentResult {Content = content.ToString(), ContentType = "application/json"};
        }

        /// <summary>
        /// returns our standard page not found view
        /// </summary>
        protected ViewResult PageNotFound()
        {
            Response.StatusCode = (int) HttpStatusCode.NotFound;
            return View("PageNotFound");
        }

        protected ViewResult PageBadRequest()
        {
            Response.StatusCode = (int) HttpStatusCode.BadRequest;
            return View("Error");
        }

        /// <summary>
        /// Answers the string "404" with response code 404.
        /// </summary>
        protected ContentResult TextPlainNotFound()
        {
            return TextPlainNotFound("404");
        }

        protected ContentResult TextPlainNotFound(string message)
        {
            Response.StatusCode = (int) HttpStatusCode.NotFound;
            return TextPlain(message);
        }

        /// <summary>
        /// Answers a null object w/ content type as "application/json" and a response code 404.
        /// </summary>
        protected JsonResult JsonNotFound()
        {
            return JsonNotFound(null);
        }

        protected JsonResult JsonNotFound(object toSerialize)
        {
            Response.StatusCode = (int) HttpStatusCode.NotFound;
            return Json(toSerialize);
        }

        protected JsonResult JsonError(string message)
        {
            Response.StatusCode = (int) HttpStatusCode.BadRequest;
            return Json(new {ErrorMessage = message});
        }

        protected JsonResult JsonError(object toSerialize)
        {
            Response.StatusCode = (int) HttpStatusCode.BadRequest;
            return Json(toSerialize);
        }

        protected JsonpResult Jsonp(object data)
        {
            return Jsonp(data, null /* contentType */);
        }

        protected JsonpResult Jsonp(object data, string contentType)
        {
            return Jsonp(data, contentType, null /*contentEncoding */);
        }

        protected JsonpResult Jsonp(object data, string contentType, Encoding contentEncoding)
        {
            return new JsonpResult
                       {
                           Data = data,
                           ContentType = contentType,
                           ContentEncoding = contentEncoding
                       };
        }

        /// <summary>
        /// This is required to support MVC2's new "security" feature 
        /// see:  http://stackoverflow.com/questions/1663221/asp-net-mvc-2-0-jsonrequestbehavior-global-setting
        /// </summary>
        protected new JsonResult Json(object data)
        {
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Answers an HTML ContentResult with the current Response's StatusCode as 500.
        /// </summary>
        protected ContentResult ContentError(string message)
        {
            Response.StatusCode = (int) HttpStatusCode.InternalServerError;
            return Content(message);
        }


        private static readonly Regex _botUserAgent =
            new Regex(@"googlebot/\d|msnbot/\d|slurp/\d|jeeves/teoma|ia_archiver|ccbot/\d|yandex/\d|twiceler-\d",
                      RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// returns true if the current request is from a search engine, based on the User-Agent header
        /// </summary>
        protected bool IsSearchEngine()
        {
            if (Request.UserAgent.IsNullOrEmpty()) return false;
            return _botUserAgent.IsMatch(Request.UserAgent);
        }


        /// <summary>
        /// known good bot DNS lookups:  
        ///   66.249.68.73     crawl-66-249-68-73.googlebot.com  
        ///   66.235.124.58    crawler5107.ask.com  
        ///   65.55.104.157    msnbot-65-55-104-157.search.msn.com 
        /// </summary>
        private static readonly Regex _botDns = new Regex(@"(googlebot\.com|ask\.com|msn\.com)$",
                                                          RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture |
                                                          RegexOptions.Compiled);

        /// <summary>
        /// returns true if the current request is from a search engine, based on the User-Agent header *AND* a reverse DNS check
        /// </summary>
        protected bool IsSearchEngineDns()
        {
            if (!IsSearchEngine()) return false;
            string s = GetHostName();
            return _botDns.IsMatch(s);
        }

        /// <summary>
        /// perform a DNS lookup on the current IP address with a 2 second timeout
        /// </summary>
        /// <returns></returns>
        protected string GetHostName()
        {
            return GetHostName(GetRemoteIP(), 2000);
        }

        /// <summary>
        /// perform a DNS lookup on the provided IP address, with a timeout specified in milliseconds
        /// </summary>
        protected string GetHostName(string ipAddress, int timeout)
        {
            Func<string, string> fetcher = ip => Dns.GetHostEntry(ip).HostName;
            try
            {
                IAsyncResult result = fetcher.BeginInvoke(ipAddress, null, null);
                return result.AsyncWaitHandle.WaitOne(timeout, false) ? fetcher.EndInvoke(result) : "Timeout";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }


        static bool? whiteListEnabled;
        static public bool WhiteListEnabled { 
            get {
                if (whiteListEnabled == null) {
                    whiteListEnabled = Current.DB.OpenIdWhiteLists.Count() > 0;
                }
                return whiteListEnabled.Value;
            } 
        }
    }
}