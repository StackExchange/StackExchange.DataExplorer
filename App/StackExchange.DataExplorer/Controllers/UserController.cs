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
        private static readonly HashSet<string> AllowedPreferences = new HashSet<string>
        {
            "HideSchema",
            "OrderBy"
        };

        [Route("users")]
        public ActionResult Index(int? page)
        {
            int perPage = 35;
            int currentPage = Math.Max(page ?? 1, 1);

            SetHeader("Users");
            SelectMenuItem("Users");

            int total = Current.DB.Query<int>("select count(*) from Users").First();
            var rows = Current.DB.Query<User>(@"select *, 
	(select COUNT(*) from Metadata where OwnerId = Y.Id) SavedQueriesCount,
	(select COUNT(*) from QueryExecutions  where UserId = Y.Id) QueryExecutionsCount  
from 
(
	select * from
	(	select 
		ROW_NUMBER() over (order by Login asc) as Row, 
		*
		from Users 
	) 
	as X 
	where Row > (@currentPage-1) * @perPage and Row <= @currentPage * @perPage
) Y
order by Row asc", new { currentPage, perPage });


            PagedList<User> data = new PagedList<Models.User>(rows, currentPage, perPage, false, total);

            return View(data);
        }


        [ValidateInput(false)]
        [HttpPost]
        [Route(@"users/edit/{id:\d+}", RoutePriority.High)]
        public ActionResult Edit(int id, User updatedUser)
        {
            User user = Current.DB.Users.Get(id);

            if (updatedUser.DOB < DateTime.Now.AddYears(-100) || updatedUser.DOB > DateTime.Now.AddYears(-6))
            {
                updatedUser.DOB = null;
            }

            if (user.Id == updatedUser.Id && (updatedUser.Id == CurrentUser.Id || CurrentUser.IsAdmin))
            {
                var violations = updatedUser.GetBusinessRuleViolations(ChangeAction.Update);

                if (violations.Count == 0)
                {
                    user.Login = HtmlUtilities.Safe(updatedUser.Login);
                    user.AboutMe = updatedUser.AboutMe;
                    user.DOB = updatedUser.DOB;
                    user.Email = HtmlUtilities.Safe(updatedUser.Email);
                    user.Website = HtmlUtilities.Safe(updatedUser.Website);
                    user.Location = HtmlUtilities.Safe(updatedUser.Location);

                    Current.DB.Users.Update(user.Id, new { user.Login, user.AboutMe, user.DOB, user.Email, user.Website, user.Location });

                    return Redirect("/users/" + user.Id);
                }
                else
                {
                    foreach (var violation in violations)
                        ModelState.AddModelError(violation.PropertyName, violation.ErrorMessage);

                    return Edit(user.Id);
                }
            }
            else
            {
                return Redirect("/");
            }
        }

        [HttpGet]
        [Route(@"users/edit/{id:\d+}", RoutePriority.High)]
        public ActionResult Edit(int id)
        {
            User user = Current.DB.Users.Get(id);
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
            else
            {
                return Redirect("/");
            }
        }

        [HttpPost]
        [Route(@"users/save-preference/{id:\d+}/{preference}")]
        public ActionResult SavePreference(int id, string preference, string value)
        {
            if (!AllowedPreferences.Contains(preference)) {
                return ContentError("Invalid preference");
            }

            User user = Current.DB.Users.Get(id);

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

        [Route(@"users/{id:INT}/{name?}")]
        public ActionResult Show(int id, string name, string order_by, int? page)
        {
            User user = Current.DB.Users.Get(id);
            if (user == null)
            {
                return PageNotFound();
            }
            // if this user has a display name, and the title is missing or does not match, permanently redirect to it
            if (user.UrlTitle.HasValue() && (string.IsNullOrEmpty(name) || name != user.UrlTitle))
            {
                return PageMovedPermanentlyTo(string.Format("/users/{0}/{1}",user.Id, HtmlUtilities.URLFriendly(user.Login)) + Request.Url.Query);
            }

            Database db = Current.DB;

            SetHeader(user.Login);
            SelectMenuItem("Users");

            order_by = order_by ?? "edited";

            ViewData["UserQueryHeaders"] = new SubHeader
            {
                Items = new List<SubHeaderViewData>
                {
                    new SubHeaderViewData
                    {
                        Description = "edited",
                        Title = "Recently edited queries",
                        Href =
                            "/users/" + user.Id + "?order_by=edited",
                        Selected = (order_by == "edited")
                    },
                    new SubHeaderViewData
                    {
                        Description = "favorite",
                        Title = "Favorite queries",
                        Href =
                            "/users/" + user.Id +
                            "?order_by=favorite",
                        Selected = (order_by == "favorite")
                    },
                    new SubHeaderViewData
                    {
                        Description = "recent",
                        Title = "Recently executed queries",
                        Href =
                            "/users/" + user.Id + "?order_by=recent",
                        Selected = (order_by == "recent")
                    }
                }
            };

            page = Math.Max(page ?? 1, 1);
            int? pagesize = 15; // In case we decide to make this a query param

            int start = ((page.Value - 1) * pagesize.Value) + 1;
            int finish = page.Value * pagesize.Value;
            bool useLatest = false;
            string message;
            var builder = new SqlBuilder();
            var pager = builder.AddTemplate(@"
                SELECT
                    *
                FROM
                    (
                        SELECT
                            /**select**/, ROW_NUMBER() OVER(/**orderby**/) AS RowNumber
                        FROM
                            Queries query
                            /**join**/
                            /**leftjoin**/
                            /**where**/
                    ) AS results
                WHERE
                    RowNumber BETWEEN @start AND @finish
                ORDER BY
                    RowNumber",
                new { start = start, finish = finish }
            );
            var counter = builder.AddTemplate("SELECT COUNT(*) FROM Queries query /**join**/ /**leftjoin**/ /**where**/");

            if (order_by == "recent")
            {
                builder.Select("execution.RevisionId AS Id");
                builder.Select("execution.LastRun");
                builder.Select("site.Name AS SiteName");
                builder.Join("QueryExecutions execution ON execution.QueryId = query.Id");
                builder.Join("Sites site ON site.Id = execution.SiteId");
                builder.Join("Revisions ON Revisions.Id = execution.RevisionId AND execution.UserId = @user", new { user = id });
                builder.Join(@"
                    Metadata metadata ON
                    (
                        metadata.RevisionId = Revisions.RootId AND
                        metadata.OwnerId = Revisions.OwnerId
                    ) OR (
                        metadata.RevisionId = Revisions.Id AND
                        metadata.OwnerId = Revisions.OwnerId AND
                        Revisions.RootId IS NULL
                    ) OR (
                        metadata.RevisionId = Revisions.Id AND
                        metadata.OwnerId IS NULL AND
                        Revisions.OwnerId IS NULL
                    )"
                );
                builder.OrderBy("execution.LastRun DESC");

                message = user.Id == CurrentUser.Id ? 
                    "You have never ran any queries" : "No queries ran recently";
            }
            else
            {
                builder.Select("metadata.RevisionId AS Id");
                builder.Select("metadata.LastActivity AS LastRun");
                builder.Join("Metadata metadata on metadata.LastQueryId = query.Id");

                if (order_by == "favorite")
                {
                    builder.Join(@"
                        Votes ON
                        Votes.RootId = metadata.RevisionId AND
                        (
                            Votes.OwnerId = metadata.OwnerId OR
                            (Votes.OwnerId IS NULL AND metadata.OwnerID IS NULL)
                        ) AND
                        Votes.UserId = @user AND
                        Votes.VoteTypeId = @vote",
                        new { user = id, vote = (int)VoteType.Favorite }
                    );
                    builder.OrderBy("metadata.Votes DESC");

                    useLatest = true;
                    message = user.Id == CurrentUser.Id ?
                        "You have no favorite queries, click the star icon on a query to favorite it" : "No favorites";
                } else {
                    builder.Where("metadata.OwnerId = @user", new { user = id });
                    builder.Where("metadata.Hidden = 0");
                    builder.OrderBy("metadata.LastActivity DESC");

                    message = user.Id == CurrentUser.Id ?
                        "You haven't edited any queries" : "No queries";
                }
            }

            builder.Select("[user].Id as CreatorId");
            builder.Select("[user].Login as CreatorLogin");
            builder.Select("metadata.Title AS Name");
            builder.Select("metadata.[Description] AS [Description]");
            builder.Select("metadata.Votes AS FavoriteCount");
            builder.Select("metadata.Views AS Views");
            builder.Select("query.QueryBody AS [SQL]");
            builder.LeftJoin("Users [user] ON [user].Id = metadata.OwnerId");

            var queries = Current.DB.Query<QueryExecutionViewData>(
                pager.RawSql,
                pager.Parameters
            ).Select<QueryExecutionViewData, QueryExecutionViewData>(
                (view) =>
                {
                    view.UseLatestLink = useLatest;
                    view.SiteName = (view.SiteName ?? Site.Name).ToLower();

                    return view;
                }
            );
            int total = Current.DB.Query<int>(counter.RawSql, counter.Parameters).First();

            ViewData["Href"] = string.Format("/users/{0}/{1}", user.Id, HtmlUtilities.URLFriendly(user.Login)) + "?order_by=" + order_by;
            ViewData["Queries"] = new PagedList<QueryExecutionViewData>(queries, page.Value, pagesize.Value, false, total);

            if (!queries.Any())
            {
                ViewData["EmptyMessage"] = message;
            }

            return View(user);
        }
    }
}