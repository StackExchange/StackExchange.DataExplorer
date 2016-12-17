using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;
using System.Web;

namespace StackExchange.DataExplorer.Controllers
{
    public class StackOverflowController : Controller
    {
        private readonly List<SubHeaderViewData> _menu = new List<SubHeaderViewData>();

        private Site _site;
        public Site Site
        {
            get
            {
                if (_site == null)
                {
                    int siteId;
                    if (int.TryParse((Session["SiteId"] ?? "").ToString(), out siteId))
                    {
                        _site = Current.DB.Query<Site>("select * from Sites where Id = @siteId", new { siteId }).FirstOrDefault();
                    }
                    if (_site == null)
                    {
                        _site = Current.DB.Query<Site>("select top 1 * from Sites order by TotalQuestions desc").SingleOrDefault();  
                    }
                    if (_site == null)
                    {
                        throw new Exception("There are no sites in the Sites table. There must be at least one for anything to work!");
                    }
                }
                return _site;
            }
            set
            {
                _site = value;

                if (_site != null)
                {
                    Session["SiteId"] = _site.Id;

                    foreach (SubHeaderViewData menuItem in _menu)
                    {
                        if (menuItem.Title == "Queries")
                        {
                            menuItem.Href = "/" + _site.TinyName.ToLower() + "/queries";
                        }
                        if (menuItem.Title == "Compose Query")
                        {
                            menuItem.Href = "/" + _site.TinyName.ToLower() + "/query/new";
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
                "SELECT * FROM Sites WHERE Id = @site", new { site = siteId }
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
                CurrentUser.IPAddress = Current.RemoteIP;
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
                
                ViewData["Menu"] = _menu;
            }
        }

        public void AddMenuItem(SubHeaderViewData data) => AddMenuItem(data, -1);

        public void AddMenuItem(SubHeaderViewData data, int index)
        {
            if (index > 0)
            {
                _menu.Insert(index, data);
            }
            else
            {
                _menu.Add(data);
            }
        }

        public void SelectMenuItem(string title)
        {
            foreach (SubHeaderViewData item in _menu)
            {
                if (item.Title == title)
                {
                    item.Selected = true;
                }
            }
        }

        public void SetHeader(string title) => SetHeader(title, (SubHeaderViewData[])null);

        public void SetHeader(string title, params SubHeaderViewData[] tabs) => SetHeader(title, null, tabs);

        public void SetHeader(string title, string selected, params SubHeaderViewData[] tabs)
        {
            ViewData["Header"] = Header = new SubHeader(title) { Selected = selected, Items = tabs };
        }

        /// <summary>
        /// called when the url doesn't match any of our known routes
        /// </summary>
        protected override void HandleUnknownAction(string actionName) => PageNotFound().ExecuteResult(ControllerContext);

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
            var user = new User {IsAnonymous = true};

            if (isAuthenticated)
            {
                int id;
                if (int.TryParse(identity, out id))
                {
                    var lookup = Current.DB.Users.Get(id);
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
        /// returns ContentResult with the parameter 'content' as its payload and "text/plain" as media type.
        /// </summary>
        protected ContentResult TextPlain(object content)
        {
            return new ContentResult {Content = content.ToString(), ContentType = "text/plain"};
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
        protected ContentResult TextPlainNotFound() => TextPlainNotFound("404");

        protected ContentResult TextPlainNotFound(string message)
        {
            Response.StatusCode = (int) HttpStatusCode.NotFound;
            return TextPlain(message);
        }

        /// <summary>
        /// This is required to support MVC2's new "security" feature 
        /// see:  http://stackoverflow.com/questions/1663221/asp-net-mvc-2-0-jsonrequestbehavior-global-setting
        /// </summary>
        protected new JsonResult Json(object data) => Json(data, JsonRequestBehavior.AllowGet);

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
    }
}