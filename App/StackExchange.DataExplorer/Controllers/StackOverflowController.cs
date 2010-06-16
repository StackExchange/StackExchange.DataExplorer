using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Models;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using StackExchange.DataExplorer.Helpers;
using System.Text;
using System.Net;
using System.Web.UI;
using System.IO;
using System.Web.Security;
using StackExchange.DataExplorer.ViewModel;

namespace StackExchange.DataExplorer.Controllers {

    public class StackOverflowController : Controller {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private List<SubHeaderViewData> menu = new List<SubHeaderViewData>();

        private Site site; 
        public Site Site {
            get {
                if (site == null) {
                    int siteId = -1;
                    Int32.TryParse((Session["SiteId"] ?? "").ToString(), out siteId);
                    site = Current.DB.Sites.FirstOrDefault(s => s.Id == siteId);
                    if (site == null) {
                        site = Current.DB.Sites.First();
                    }
                }
                return site;
            }
            set {
                site = value;
                Session["SiteId"] = site.Id;
                foreach (var menuItem in menu) {
                    if (menuItem.Title == "Queries") {
                        menuItem.Href = "/" + site.Name.ToLower() + "/queries";
                    }
                    if (menuItem.Title == "Compose Query") {
                        menuItem.Href = "/" + site.Name.ToLower() + "/query/new";
                    }
                }
            }
        }


#if DEBUG
        System.Diagnostics.Stopwatch watch;
#endif

        protected override void Initialize(System.Web.Routing.RequestContext requestContext) {

#if DEBUG 
            watch = new System.Diagnostics.Stopwatch();
            watch.Start();
#endif

            Current.Controller = this; // allow code to easily find this controller
            ValidateRequest = false; // allow html/sql in form values - remember to validate!
            base.Initialize(requestContext);


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

            

            AddMenuItem(new SubHeaderViewData()
            { 
                 Description = "Home",
                 Href = "/",
                 RightAlign = false,
                 Title = "Home"
            });


            AddMenuItem(new SubHeaderViewData()
            {
                Title = "Queries",
                Description = "Queries",
                Href = "/" + Site.Name.ToLower() + "/queries"
            });

            AddMenuItem(new SubHeaderViewData()
            {
                Description = "Users",
                Href = "/users",
                RightAlign = false,
                Title = "Users"
            });

            AddMenuItem(new SubHeaderViewData()
            {
                Title = "Compose Query",
                Description = "Compose Query",
                Href = "/" + Site.Name.ToLower() + "/query/new",
                RightAlign = true
            });


            ViewData["Menu"] = menu;
        }

        public void AddMenuItem(SubHeaderViewData data) {
            AddMenuItem(data, -1);
        }

        public void AddMenuItem(SubHeaderViewData data, int index) {
            if (index > 0) {
                menu.Insert(index, data);
            } else {
                menu.Add(data);
            } 
        }

        public void SelectMenuItem(string title) {
            foreach (var item in menu) {
                if (item.Title == title) {
                    item.Selected = true;
                }
            }
        }

        public void SetHeader(string title) {
            SetHeader(title, null);
        }

        public void SetHeader(string title, params SubHeaderViewData[] tabs) {
            ViewData["Header"] = new SubHeader(title) { Items = tabs };

        }

        protected CachedResult GetCachedResults(Query query) {
            CachedResult cachedResults = null;

            var db = Current.DB;

            ParsedQuery p = new ParsedQuery(query.QueryBody, Request.Params);
            if (p.AllParamsSet) {
                cachedResults = db.CachedResults
                    .Where(r => r.QueryHash == p.ExecutionHash && r.SiteId == Site.Id)
                    .FirstOrDefault();

            }
            return cachedResults;
        }

        /// <summary>
        /// Gets the shared DataContext to be used by a Request's controllers.
        /// </summary>
        public DBContext DB {
            get { return Current.DB; }
        }


        /// <summary>
        /// Indicates that this controller is really a "subcontroller" and does not need shared messages added to the ViewData
        /// </summary>
        public bool IsSubControllerCall { get; set; }

        /// <summary>
        /// called when the url doesn't match any of our known routes
        /// </summary>
        protected override void HandleUnknownAction(string actionName) {
            PageNotFound().ExecuteResult(ControllerContext);
        }

        /// <summary>
        /// fires after the controller finishes execution
        /// </summary>
        protected override void OnActionExecuted(ActionExecutedContext filterContext) {
#if DEBUG
            log.Debug("OnActionExecuted -> " + Request.Url.PathAndQuery + " Duration: " + watch.ElapsedMilliseconds.ToString());
            System.Diagnostics.Trace.WriteLine("OnActionExecuted -> " + Request.Url.PathAndQuery + " Duration: " + watch.ElapsedMilliseconds.ToString());
#endif
            base.OnActionExecuted(filterContext);
        }

        /// <summary>
        /// fires after a View has finished execution
        /// </summary>
        protected override void OnResultExecuted(ResultExecutedContext filterContext) {
            base.OnResultExecuted(filterContext);
        }


        /// <summary>
        /// When a client IP can't be determined
        /// </summary>
        public const string UnknownIP = "0.0.0.0";

        private static Regex _ipAddress = new Regex(@"\b([0-9]{1,3}\.){3}[0-9]{1,3}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        /// <summary>
        /// returns true if this is a private network IP  
        /// http://en.wikipedia.org/wiki/Private_network
        /// </summary>
        private static bool IsPrivateIP(string s) {
            return (s.StartsWith("192.168.") || s.StartsWith("10.") || s.StartsWith("127.0.0."));
        }

        /// <summary>
        /// retrieves the IP address of the current request -- handles proxies and private networks
        /// </summary>
        public static string GetRemoteIP(NameValueCollection ServerVariables) {
            var ip = ServerVariables["REMOTE_ADDR"]; // could be a proxy -- beware
            var ipForwarded = ServerVariables["HTTP_X_FORWARDED_FOR"];

            // check if we were forwarded from a proxy
            if (ipForwarded.HasValue()) {
                ipForwarded = _ipAddress.Match(ipForwarded).Value;
                if (ipForwarded.HasValue() && !IsPrivateIP(ipForwarded))
                    ip = ipForwarded;
            }

            return ip.HasValue() ? ip : UnknownIP;
        }

        /// <summary>
        /// Answers the current request's user's ip address; checks for any forwarding proxy
        /// </summary>
        public string GetRemoteIP() {
            return GetRemoteIP(Request.ServerVariables);
        }

        /// <summary>
        /// returns true only if this request came from an internal IP address
        /// </summary>
        public bool IsInternalIp() {
#if DEBUG
            if (Current.Tier == DeploymentTier.Local) return true;
#endif
            // TODO: **BEWARE** this shouldn't really be hard-coded, but in the sites db table!
            string ip = GetRemoteIP();
            if (ip.StartsWith("69.59")) return true;
            if (ip.StartsWith("64.34.80")) return true;
            if (ip.StartsWith("10.0.0.")) return true;   // local private server IP range
            if (ip.StartsWith("127.0.0.1")) return true; // loopback IP on /scheduled/ tasks
            return false;
        }


        private User _currentUser;
        /// <summary>
        /// Gets a User object representing the current request's client.
        /// </summary>
        public User CurrentUser {
            get {
                if (_currentUser == null) InitCurrentUser();
                return _currentUser;
            }
        }

        /// <summary>
        /// When creating the CurrentUser, determines if user preferences are loaded.
        /// True by default, override to turn off (e.g. VotesController should override this property).
        /// </summary>
        protected virtual bool RequiresUserPreferences {
            get { return true; }
        }

        /// <summary>
        /// initializes current user based on the current Request's cookies/authentication status. This
        /// method could return a newly created, Anonymous User if no means of identification are found.
        /// </summary>
        protected void InitCurrentUser() {
            _currentUser = new User();
            _currentUser.IsAnonymous = true;

            if (Request.IsAuthenticated) {
                int id;
                if (Int32.TryParse(User.Identity.Name, out id)) {
                     var lookup = Current.DB.Users.FirstOrDefault(u => u.Id == id);
                     if (lookup != null) {
                         _currentUser = lookup;
                         _currentUser.IsAnonymous = false;
                     } 
                } else {
                    FormsAuthentication.SignOut();
                }
            }

            _currentUser.IPAddress = Request.UserHostAddress;
        }


        /// <summary>
        /// Answers a view page for the current site, e.g. to view the About-StackOverflow, pass in About for 'viewName'.
        /// </summary>
        protected ViewResult ViewSiteSpecific(string viewName) {
            return ViewSiteSpecific(viewName, null);
        }

        /// <summary>
        /// Answers a view page for the current site, e.g. to view the About-StackOverflow, pass in About for 'viewName'.
        /// </summary>
        protected ViewResult ViewSiteSpecific(string viewName, object viewData) {
            var name = string.Concat(viewName, "-", "s"); // Current.Site.ResourcesName);
            return viewData == null ? View(name) : View(name, viewData);

        }


        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/plain" as media type.
        /// </summary>
        protected ContentResult TextPlain(object content) {
            return new ContentResult { Content = content.ToString(), ContentType = "text/plain" };
        }

        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/html" as media type.
        /// </summary>
        protected ContentResult HtmlRaw(object content) {
            return new ContentResult { Content = content.ToString(), ContentType = "text/html" };
        }

        /// <summary>
        /// returns ContentResult with the parameter 'content' as its payload and "text/html" as media type.
        /// </summary>
        protected ContentResult JsonRaw(object content) {
            return new ContentResult { Content = content.ToString(), ContentType = "application/json" };
        }

        /// <summary>
        /// returns our standard page not found view
        /// </summary>
        protected ViewResult PageNotFound() {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return View("PageNotFound");
        }

        protected ViewResult PageBadRequest() {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return View("Error");
        }

        /// <summary>
        /// Answers the string "404" with response code 404.
        /// </summary>
        protected ContentResult TextPlainNotFound() {
            return TextPlainNotFound("404");
        }

        protected ContentResult TextPlainNotFound(string message) {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return TextPlain(message);
        }

        /// <summary>
        /// Answers a null object w/ content type as "application/json" and a response code 404.
        /// </summary>
        protected JsonResult JsonNotFound() {
            return JsonNotFound(null);
        }

        protected JsonResult JsonNotFound(object toSerialize) {
            Response.StatusCode = (int)HttpStatusCode.NotFound;
            return Json(toSerialize);
        }

        protected JsonResult JsonError(string message) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return Json(new { ErrorMessage = message });
        }

        protected JsonResult JsonError(object toSerialize) {
            Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return Json(toSerialize);
        }

        protected JsonpResult Jsonp(object data) {
            return Jsonp(data, null /* contentType */);
        }
        protected JsonpResult Jsonp(object data, string contentType) {
            return Jsonp(data, contentType, null /*contentEncoding */);
        }
        protected JsonpResult Jsonp(object data, string contentType, Encoding contentEncoding) {
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
        protected new JsonResult Json(object data) {
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Answers an HTML ContentResult with the current Response's StatusCode as 500.
        /// </summary>
        protected ContentResult ContentError(string message) {
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return Content(message);
        }


        private static readonly Regex _botUserAgent = new Regex(@"googlebot/\d|msnbot/\d|slurp/\d|jeeves/teoma|ia_archiver|ccbot/\d|yandex/\d|twiceler-\d", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// returns true if the current request is from a search engine, based on the User-Agent header
        /// </summary>
        protected bool IsSearchEngine() {
            if (Request.UserAgent.IsNullOrEmpty()) return false;
            return _botUserAgent.IsMatch(Request.UserAgent);
        }


        /// <summary>
        /// known good bot DNS lookups:  
        ///   66.249.68.73     crawl-66-249-68-73.googlebot.com  
        ///   66.235.124.58    crawler5107.ask.com  
        ///   65.55.104.157    msnbot-65-55-104-157.search.msn.com 
        /// </summary>
        private static readonly Regex _botDns = new Regex(@"(googlebot\.com|ask\.com|msn\.com)$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        /// <summary>
        /// returns true if the current request is from a search engine, based on the User-Agent header *AND* a reverse DNS check
        /// </summary>
        protected bool IsSearchEngineDns() {
            if (!IsSearchEngine()) return false;
            string s = GetHostName();
            return _botDns.IsMatch(s);
        }

        /// <summary>
        /// perform a DNS lookup on the current IP address with a 2 second timeout
        /// </summary>
        /// <returns></returns>
        protected string GetHostName() {
            return GetHostName(GetRemoteIP(), 2000);
        }

        /// <summary>
        /// perform a DNS lookup on the provided IP address, with a timeout specified in milliseconds
        /// </summary>
        protected string GetHostName(string ipAddress, int timeout) {
            Func<string, string> fetcher = ip => System.Net.Dns.GetHostEntry(ip).HostName;
            try {
                var result = fetcher.BeginInvoke(ipAddress, null, null);
                return result.AsyncWaitHandle.WaitOne(timeout, false) ? fetcher.EndInvoke(result) : "Timeout";
            } catch (Exception ex) {
                return ex.GetType().Name;
            }
        }


        /// <summary>
        /// Render a Partial View (MVC User Control, .ascx) to a string using the given ViewData.
        /// </summary>
        public static string RenderPartialToString(string controlName, object viewData) {
            var vp = new ViewPage { ViewData = new ViewDataDictionary(viewData) };
            vp.Controls.Add(vp.LoadControl(controlName));

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var tw = new HtmlTextWriter(sw))
                vp.RenderControl(tw);

            return sb.ToString();
        }

    }

}