using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;
using Dapper;

namespace StackExchange.DataExplorer.Controllers
{
    public class UserController : StackOverflowController
    {
        private static readonly HashSet<string> _allowedPreferences = new HashSet<string>
        {
            "HideSchema",
            "OrderBy"
        };

        [StackRoute("users")]
        public ActionResult Index(string order_by, int? page, string search)
        {
            SetHeader("Users", order_by, 
                new SubHeaderViewData
                {
                    Description = "all",
                    Title = "All registered users",
                    Href = "/users?order_by=all",
                    Default = true
                },
                new SubHeaderViewData
                {
                    Description = "active",
                    Title = "Users who have been active in the last 30 days",
                    Href = "/users?order_by=active"
                }
            );
            SelectMenuItem("Users");

            var users = GetUserList(Header.Selected, page, search);
            ViewData["SearchUser"] = search;

            return View(users);
        }

        [StackRoute("users/search")]
        public ActionResult Search(string order_by, string search)
        {
            var users = GetUserList(order_by == "active" ? "active" : "all", null, search);

            return PartialView("~/Views/Shared/UserList.cshtml", users);
        }

        private PagedList<User> GetUserList(string selected, int? page, string search)
        {
            int perPage = 36;
            int currentPage = Math.Max(page ?? 1, 1);
            var builder = new SqlBuilder();
            var pager = builder.AddTemplate(@"
                SELECT *,
                    (SELECT COUNT(*) FROM QuerySets WHERE OwnerId = Y.Id) SavedQueriesCount,
                    (SELECT COUNT(*) FROM RevisionExecutions WHERE UserId = Y.Id) QueryExecutionsCount
                FROM
                (
                    SELECT * FROM
                    (
                        SELECT ROW_NUMBER() OVER (/**orderby**/) AS Row, Users.Id, Users.Login, Users.Email,
                               Users.IPAddress, Users.IsAdmin, Users.CreationDate /**select**/
                          FROM Users /**join**/ /**where**/
                    ) AS X
                    WHERE Row > @start AND Row <= @finish
                ) AS Y
                ORDER BY Row ASC",
                new { start = (currentPage - 1) * perPage, finish = currentPage * perPage }
            );
            var counter = builder.AddTemplate("SELECT COUNT(*) FROM Users /**join**/ /**where**/");

            if (selected == "all")
            {
                builder.OrderBy("Login ASC");
            }
            else
            {
                var activePeriod = 30; // Last 30 days

                // We should probably just be...actually recording the user's LastActivityDate,
                // instead of performing this join all the time
                builder.Select(", LastRun AS LastActivityDate");
                builder.Join(@"(
                    SELECT UserId, MAX(LastRun) AS LastRun FROM RevisionExecutions GROUP BY UserId
                ) AS LastExecutions ON LastExecutions.UserId = Users.Id");
                builder.OrderBy("LastRun DESC");
                builder.Where("LastRun >= @since", new { since = DateTime.UtcNow.AddDays(-activePeriod).Date });
            }

            var url = "/users?order_by=" + selected;
            ViewData["SearchHref"] = "/users/search?order_by=" + selected;

            if (search.HasValue() && search.Length > 2)
            {
                url += "&search=" + HtmlUtilities.UrlEncode(search);
                ViewData["UserSearch"] = search;

                builder.Where("Login LIKE @search", new { search = '%' + search + '%' });
            }

            ViewData["Href"] = url;

            var rows = Current.DB.Query<User>(pager.RawSql, pager.Parameters);
            var total = Current.DB.Query<int>(counter.RawSql, counter.Parameters).First();
            var users = new PagedList<User>(rows, currentPage, perPage, false, total);

            return users;
        }


        [ValidateInput(false)]
        [ValidateAntiForgeryToken]
        [HttpPost]
        [StackRoute(@"users/edit/{id:\d+}", RoutePriority.High)]
        public ActionResult Edit(int id, User updatedUser)
        {
            User user = Current.DB.Users.Get(id);

            if (user.Id == updatedUser.Id && (updatedUser.Id == CurrentUser.Id || CurrentUser.IsAdmin))
            {
                var violations = updatedUser.GetBusinessRuleViolations(ChangeAction.Update);

                if (violations.Count == 0)
                {
                    var snapshot = Snapshotter.Start(user);
                    user.Login = HtmlUtilities.Safe(updatedUser.Login);
                    user.AboutMe = updatedUser.AboutMe;
                    user.Email = HtmlUtilities.Safe(updatedUser.Email);
                    user.Website = HtmlUtilities.Safe(updatedUser.Website);
                    user.Location = HtmlUtilities.Safe(updatedUser.Location);

                    // Preferences are updated separately, so we should likely do this elsewhere instead...
                    // Can likely move it out if we have introduce a fancier OpenID management panel like
                    // the network has.
                    user.EnforceSecureOpenId = updatedUser.EnforceSecureOpenId;

                    var diff = snapshot.Diff();

                    if (diff.ParameterNames.Any())
                    {
                        Current.DB.Users.Update(user.Id, snapshot.Diff());
                    }

                    return Redirect("/users/" + user.Id);
                }
                else
                {
                    foreach (var violation in violations)
                        ModelState.AddModelError(violation.PropertyName, violation.ErrorMessage);

                    return Edit(user.Id);
                }
            }

            return Redirect("/");
        }

        [HttpGet]
        [StackRoute(@"users/edit/{id:\d+}", RoutePriority.High)]
        public ActionResult Edit(int id)
        {
            var user = Current.DB.Users.Get(id);
            if (user == null)
            {
                return PageNotFound();
            }

            if (user.Id == CurrentUser.Id || CurrentUser.IsAdmin)
            {
                SetHeader(user.Login + " - Edit");
                SelectMenuItem("Users");

                return View(user);
            }

            return Redirect("/");
        }

        [HttpPost]
        [StackRoute(@"users/save-preference/{id:\d+}/{preference}")]
        public ActionResult SavePreference(int id, string preference, string value)
        {
            if (!_allowedPreferences.Contains(preference)) {
                return ContentError("Invalid preference");
            }

            var user = Current.DB.Users.Get(id);
            if (user == null || (user.Id != CurrentUser.Id && !CurrentUser.IsAdmin))
            {
                return ContentError("Invalid action");
            }

            if (preference == "HideSchema")
            {
                user.HideSchema = value == "true";
            }

            return Content("ok");
        }

        [StackRoute(@"users/{id:INT}/{name?}")]
        public ActionResult Show(int id, string name, string order_by, int? page)
        {
            var user = !Current.User.IsAnonymous && Current.User.Id == id ? Current.User : Current.DB.Users.Get(id);

            if (user == null)
            {
                return PageNotFound();
            }
            
            // if this user has a display name, and the title is missing or does not match, permanently redirect to it
            if (user.UrlTitle.HasValue() && (string.IsNullOrEmpty(name) || name != user.UrlTitle))
            {
                return PageMovedPermanentlyTo($"/users/{user.ProfilePath}{Request.Url.Query}");
            }

            SetHeader(user.Login);
            SelectMenuItem("Users");

            var profileTabs = new SubHeader
            {
                Selected = order_by,
                Items = new List<SubHeaderViewData>
                {
                    new SubHeaderViewData
                    {
                        Description = "edited",
                        Title = "Recently edited queries",
                        Href = "/users/" + user.ProfilePath + "?order_by=edited",
                        Default = true,
                    },
                    new SubHeaderViewData
                    {
                        Description = "favorite",
                        Title = "Favorite queries",
                        Href = "/users/" + user.ProfilePath + "?order_by=favorite"
                    },
                    new SubHeaderViewData
                    {
                        Description = "recent",
                        Title = "Recently executed queries",
                        Href = "/users/" + user.ProfilePath + "?order_by=recent"
                    }
                }
            };
            ViewData["UserQueryHeaders"] = profileTabs;

            page = Math.Max(page ?? 1, 1);
            int? pagesize = 15; // In case we decide to make this a query param

            int start = ((page.Value - 1) * pagesize.Value) + 1;
            int finish = page.Value * pagesize.Value;
            string message;
            var builder = new SqlBuilder();
            var pager = builder.AddTemplate(@"
                SELECT *
                FROM (SELECT /**select**/, ROW_NUMBER() OVER(/**orderby**/) AS RowNumber
                        FROM Queries q
                             /**join**/
                             /**leftjoin**/
                             /**where**/
                    ) AS results
                WHERE RowNumber BETWEEN @start AND @finish
             ORDER BY RowNumber",
                new {start, finish}
            );
            var counter = builder.AddTemplate("SELECT COUNT(*) FROM Queries q /**join**/ /**leftjoin**/ /**where**/");

            if (order_by == "recent")
            {
                builder.Select("re.RevisionId AS Id");
                builder.Select("re.LastRun");
                builder.Select("s.TinyName AS SiteName");
                builder.Where("re.UserId = @user", new { user = id });
                builder.Join("Revisions r ON r.QueryId = q.Id");
                builder.Join("RevisionExecutions re ON re.RevisionId = r.Id");
                builder.Join("Sites s ON s.Id = re.SiteId");
                builder.Join(@"QuerySets qs ON qs.Id = r.OriginalQuerySetId");
                builder.OrderBy("re.LastRun DESC");

                message = user.Id == CurrentUser.Id ? 
                    "You have never ran any queries" : "No queries ran recently";
            }
            else
            {
                builder.Select("qs.CurrentRevisionId AS Id");
                builder.Select("qs.LastActivity AS LastRun");
                builder.Join("Revisions r on r.QueryId = q.Id");
                builder.Join("QuerySets qs on qs.CurrentRevisionId = r.Id");

                if (order_by == "favorite")
                {
                    builder.Join(@"
                        Votes v ON
                        v.QuerySetId = qs.Id AND
                        v.UserId = @user AND
                        v.VoteTypeId = @vote",
                        new { user = id, vote = (int)VoteType.Favorite }
                    );
                    builder.OrderBy("v.Id DESC");
                    message = user.Id == CurrentUser.Id ?
                        "You have no favorite queries, click the star icon on a query to favorite it" : "No favorites";
                } else {
                    builder.Where("qs.OwnerId = @user", new { user = id });
                    builder.Where("qs.Hidden = 0");
                    builder.OrderBy("qs.LastActivity DESC");

                    message = user.Id == CurrentUser.Id ?
                        "You haven't edited any queries" : "No queries";
                }
            }

            builder.Select("qs.Id as QuerySetId");
            builder.Select("u.Id as CreatorId");
            builder.Select("u.Login as CreatorLogin");
            builder.Select("qs.Title AS Name");
            builder.Select("qs.[Description] AS [Description]");
            builder.Select("qs.Votes AS FavoriteCount");
            builder.Select("qs.Views AS Views");
            builder.Select("q.QueryBody AS [SQL]");
            builder.LeftJoin("Users u ON u.Id = qs.OwnerId");

            var queries = Current.DB.Query<QueryExecutionViewData>(
                pager.RawSql,
                pager.Parameters
            ).Select(
                view =>
                {
                    view.SiteName = (view.SiteName ?? Site.TinyName).ToLower();
                    return view;
                }
            );
            int total = Current.DB.Query<int>(counter.RawSql, counter.Parameters).First();

            ViewData["Href"] = $"/users/{user.ProfilePath}?order_by={profileTabs.Selected}";
            ViewData["Queries"] = new PagedList<QueryExecutionViewData>(queries, page.Value, pagesize.Value, false, total);

            if (!queries.Any())
            {
                ViewData["EmptyMessage"] = message;
            }

            return View(user);
        }
    }
}