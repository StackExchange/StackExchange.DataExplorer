using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;
using Dapper;
using System.Web;

namespace StackExchange.DataExplorer.Controllers
{
    public class StackOverflowController : Controller
    {
        private readonly List<SubHeaderViewData> menu = new List<SubHeaderViewData>();

        private Site site;

        public Site Site
        {
            get
            {
                if (site == null)
                {
                    int siteId = -1;
                    if (Int32.TryParse((Session["SiteId"] ?? "").ToString(), out siteId))
                    {
                        site = Current.DB.Query<Site>("select * from Sites where Id = @siteId", new { siteId }).FirstOrDefault();
                    }
                    if (site == null)
                    {
                        site = Current.DB.Query<Site>("select top 1 * from Sites order by TotalQuestions desc").SingleOrDefault();  
                    }
                    if (site == null)
                    {
                        throw new Exception("There are no sites in the Sites table. There must be at least one for anything to work!");
                    }
                }
                return site;
            }
            set
            {
                site = value;

                if (site != null)
                {
                    Session["SiteId"] = site.Id;

                    foreach (SubHeaderViewData menuItem in menu)
                    {
                        if (menuItem.Title == "Queries")
                        {
                            menuItem.Href = "/" + site.TinyName.ToLower() + "/queries";
                        }
                        if (menuItem.Title == "Compose Query")
                        {
                            menuItem.Href = "/" + site.TinyName.ToLower() + "/query/new";
                        }
                    }
                }
            }
        }

        public SubHeader Header { get; private set; }

        public bool TryGetSite(string sitename, out Site site)
        {
            site = Current.DB.Query<Models.Site>(
                "SELECT * from Sites WHERE LOWER(TinyName) = @sitename OR LOWER(Name) = @sitename", new { sitename }
            ).FirstOrDefault();

            return site != null && site.TinyName.ToLower() == sitename;
        }

        public Site GetSite(int siteId)
        {
            return Current.DB.Query<Site>(
                "SELECT * FROM Sites WHERE Id = @site",
                new
                {
                    site = siteId
                }
            ).FirstOrDefault();
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

            if (AppSettings.EnableWhiteList && !(this is AccountController) && CurrentUser.IsAnonymous) {
                requestContext.HttpContext.Response.Redirect("/account/login?returnurl=" + Request.RawUrl);
            }

            if (!CurrentUser.IsAnonymous && (CurrentUser.LastSeenDate == null || (DateTime.UtcNow - CurrentUser.LastSeenDate.Value).TotalSeconds > 120))
            {
                CurrentUser.LastSeenDate = DateTime.UtcNow;
                CurrentUser.IPAddress = GetRemoteIP();
                Current.DB.Users.Update(CurrentUser.Id, new { CurrentUser.LastSeenDate, CurrentUser.IPAddress });
            }

            if (!requestContext.HttpContext.Request.IsAjaxRequest())
            {
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
                    Href = "/" + Site.TinyName.ToLower() + "/queries"
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
                    Id = "compose-button",
                    Title = "Compose Query",
                    Description = "Compose Query",
                    Href = "/" + Site.TinyName.ToLower() + "/query/new",
                    RightAlign = true
                });


                ViewData["Menu"] = menu;
            }
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
            SetHeader(title, (SubHeaderViewData[])null);
        }

        public void SetHeader(string title, params SubHeaderViewData[] tabs)
        {
            SetHeader(title, null, tabs);
        }

        public void SetHeader(string title, string selected, params SubHeaderViewData[] tabs)
        {
            ViewData["Header"] = Header = new SubHeader(title) { Selected = selected, Items = tabs };
        }

        /// <summary>
        /// Gets the shared DataContext to be used by a Request's controllers.
        /// </summary>
        public DataExplorerDatabase DB
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
            _currentUser = GetCurrentUser(Request, User.Identity.Name);
        }

        public static User GetCurrentUser(HttpRequest request, string identity)
        {
            return GetCurrentUser(request.IsAuthenticated, request.UserHostAddress, identity);
        }

        public static User GetCurrentUser(HttpRequestBase request, string identity)
        {
            return GetCurrentUser(request.IsAuthenticated, request.UserHostAddress, identity);
        }

        private static User GetCurrentUser(bool isAuthenticated, string userHostAddress, string identity)
        {
            var user = new User();
            user.IsAnonymous = true;

            if (isAuthenticated)
            {
                int id;
                if (Int32.TryParse(identity, out id))
                {
                    User lookup = Current.DB.Users.Get(id);
                    if (lookup != null)
                    {
                        user = lookup;
                        user.IsAnonymous = false;
                    }
                }
                else
                {
                    FormsAuthentication.SignOut();
                }
            }

            user.IPAddress = userHostAddress;
            return user;
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

        /// <summary>
        /// returns a 301 permanent redirect
        /// </summary>
        /// <returns></returns>
        protected ContentResult PageMovedPermanentlyTo(string url)
        {
            Response.RedirectLocation = url;
            Response.StatusCode = (int) HttpStatusCode.MovedPermanently;
            return null;
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

    }
}