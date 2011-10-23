using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;

namespace StackExchange.DataExplorer.Controllers
{
    public class UserController : StackOverflowController
    {
        private static readonly HashSet<string> AllowedPreferences = new HashSet<string>
        {
            "HideSchema"
        };

        [Route("users")]
        public ActionResult Index(int? page)
        {
            int currentPage = Math.Max(page ?? 1, 1);

            SetHeader("Users");
            SelectMenuItem("Users");

            ViewData["PageNumbers"] = new PageNumber("/users?page=-1", Convert.ToInt32(Math.Ceiling(Current.DB.Users.Count() / 35m)), 50,
                                                     currentPage - 1, "pager fr");

            PagedList<User> data = Current.DB.Users.OrderBy(u => u.Login).ToPagedList(currentPage, 35);
            return View(data);
        }


        [ValidateInput(false)]
        [HttpPost]
        [Route(@"users/edit/{id:\d+}", RoutePriority.High)]
        public ActionResult Edit(int id, User updatedUser)
        {
            User user = Current.DB.Users.First(u => u.Id == id);

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

                    Current.DB.SubmitChanges();

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
            User user = Current.DB.Users.FirstOrDefault(u => u.Id == id);
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

            User user = Current.DB.Users.FirstOrDefault(u => u.Id == id);

            if (user == null || (user.Id != CurrentUser.Id && !CurrentUser.IsAdmin))
            {
                return ContentError("Invalid action");
            }

            if (preference == "HideSchema")
            {
                user.HideSchema = value == "true";
            }

            Current.DB.SubmitChanges();

            return Content("ok");
        }

        [Route(@"users/{id:INT}/{name?}")]
        public ActionResult Show(int id, string name, string order_by)
        {
            User user = Current.DB.Users.FirstOrDefault(row => row.Id == id);
            if (user == null)
            {
                return PageNotFound();
            }
            // if this user has a display name, and the title is missing or does not match, permanently redirect to it
            if (user.UrlTitle.HasValue() && (string.IsNullOrEmpty(name) || name != user.UrlTitle))
            {
                return PageMovedPermanentlyTo(string.Format("/users/{0}/{1}",user.Id, HtmlUtilities.URLFriendly(user.Login)) + Request.Url.Query);
            }

            DBContext db = Current.DB;

            SetHeader(user.Login);
            SelectMenuItem("Users");

            order_by = order_by ?? "saved";

            ViewData["UserQueryHeaders"] = new SubHeader
            {
                Items = new List<SubHeaderViewData>
                {
                    new SubHeaderViewData
                    {
                        Description = "saved",
                        Title = "Saved Queries",
                        Href =
                            "/users/" + user.Id + "?order_by=saved",
                        Selected = (order_by == "saved")
                    },
                    new SubHeaderViewData
                    {
                        Description = "favorite",
                        Title = "Favorite Queries",
                        Href =
                            "/users/" + user.Id +
                            "?order_by=favorite",
                        Selected = (order_by == "favorite")
                    },
                    new SubHeaderViewData
                    {
                        Description = "recent",
                        Title = "Recent Queries",
                        Href =
                            "/users/" + user.Id + "?order_by=recent",
                        Selected = (order_by == "recent")
                    }
                }
            };

            IEnumerable<QueryExecutionViewData> queries;

            if (order_by == "recent")
            {
                queries = Current.DB.Query<Metadata, QueryExecution, QueryExecutionViewData>(@"
                    ",
                    (metadata, execution) =>
                    {
                        return new QueryExecutionViewData
                        {

                        };
                    }
                );
            }
            else if (order_by == "favorite")
            {
                queries = from v in db.Votes
                            join e in db.SavedQueries on v.SavedQueryId equals e.Id
                            join q in db.Queries on e.QueryId equals q.Id
                            join s in db.Sites on e.SiteId equals s.Id
                            where v.UserId == id && v.VoteTypeId == (int) VoteType.Favorite
                            orderby e.LastEditDate descending
                            select new QueryExecutionViewData
                                        {
                                            LastRun = e.LastEditDate ?? DateTime.Now,
                                            SiteName = s.Name.ToLower(),
                                            SQL = q.QueryBody,
                                            Id = e.Id,
                                            Name = e.Title,
                                            Description = e.Description
                                        };
            }
            else
            {
                queries = from e in db.SavedQueries
                            join q in db.Queries on e.QueryId equals q.Id
                            join s in db.Sites on e.SiteId equals s.Id
                            where e.UserId == id && !(e.IsDeleted ?? false)
                            orderby e.LastEditDate descending
                            select new QueryExecutionViewData
                                        {
                                            LastRun = e.LastEditDate ?? DateTime.Now,
                                            SiteName = s.Name.ToLower(),
                                            SQL = q.QueryBody,
                                            Id = e.Id,
                                            Name = e.Title,
                                            Description = e.Description
                                        };
            }

            QueryExecutionViewData[] queriesArray = queries.Take(50).ToArray();

            ViewData["Queries"] = queriesArray;
            if (queriesArray.Length == 0)
            {
                if (order_by == "recent")
                {
                    if (user.Id == CurrentUser.Id)
                    {
                        ViewData["EmptyMessage"] = "You have never ran any queries";
                    }
                    else
                    {
                        ViewData["EmptyMessage"] = "No queries ran recently";
                    }
                }
                else if (order_by == "favorite")
                {
                    if (user.Id == CurrentUser.Id)
                    {
                        ViewData["EmptyMessage"] =
                            "You have no favorite queries, click the star icon on a query to favorite it";
                    }
                    else
                    {
                        ViewData["EmptyMessage"] = "No favorites";
                    }
                }
                else
                {
                    if (user.Id == CurrentUser.Id)
                    {
                        ViewData["EmptyMessage"] = "You have no saved queries, you can save any query after you run it";
                    }
                    else
                    {
                        ViewData["EmptyMessage"] = "No saved queries";
                    }
                }
            }
            return View(user);
        }
    }
}