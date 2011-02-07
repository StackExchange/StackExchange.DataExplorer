using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;

namespace StackExchange.DataExplorer.Controllers
{
    public class SavedQueryController : StackOverflowController
    {
        [Route(@"{sitename}/st/{id:\d+}/{slug?}")]
        public ActionResult ShowText(int id, string sitename)
        {
            return ProcessShow(id, sitename, "text");
        }

        [Route(@"{sitename}/s/{id:\d+}/{slug?}")]
        public ActionResult Show(int id, string sitename)
        {
            return ProcessShow(id, sitename, "default");
        }

        private ActionResult ProcessShow(int id, string sitename, string format)
        {
            DBContext db = Current.DB;

            Site = GetSite(sitename);

            ViewData["GuessedUserId"] = Site.GuessUserId(CurrentUser);
            SelectMenuItem("Queries");

            SavedQuery savedQuery = db.SavedQueries.FirstOrDefault(q => q.Id == id);
            if (savedQuery == null)
            {
                return PageNotFound();
            }

            SetHeader(HtmlUtilities.Encode(savedQuery.Title));
            int totalVotes =
                db.Votes.Where(v => v.SavedQueryId == id && v.VoteTypeId == (int) VoteType.Favorite).Count();

            var voting = new QueryVoting
                             {
                                 TotalVotes = totalVotes
                             };

            if (!Current.User.IsAnonymous)
            {
                voting.HasVoted = (
                                      db.Votes.FirstOrDefault(v => v.SavedQueryId == id
                                                                   && v.UserId == Current.User.Id
                                                                   && v.VoteTypeId == (int) VoteType.Favorite)
                                  ) != null;
            }

            ViewData["QueryVoting"] = voting;
            ViewData["Sites"] = Current.DB.Sites.ToList();
            ViewData["LoggedOn"] = Current.User.IsAnonymous ? "false" : "true";

            CachedResult cachedResults = GetCachedResults(savedQuery.Query);
            if (cachedResults != null)
            {
                ViewData["cached_results"] = cachedResults;

                if (format == "text")
                {
                    if (cachedResults.Results != null)
                    {
                        cachedResults.Results = QueryResults.FromJson(cachedResults.Results).ToTextResults().ToJson();
                    }
                }
            }

            if (!IsSearchEngine())
            {
                QueryViewTracker.TrackQueryView(GetRemoteIP(), savedQuery.Query.Id);
            }

            return View("Show", savedQuery);
        }

        [Route("saved_query/delete", HttpVerbs.Post)]
        public ActionResult Delete(int id)
        {
            SavedQuery query = Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null && (query.UserId == Current.User.Id || Current.User.IsAdmin))
            {
                if (query.IsFeatured ?? false)
                {
                    return Json("Query is featured, delete is not permitted");
                }
                query.IsDeleted = true;
                Current.DB.SubmitChanges();
            }

            return Json("success");
        }

        [Route("saved_query/undelete", HttpVerbs.Post)]
        public ActionResult Undelete(int id)
        {
            SavedQuery query = Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null && (query.UserId == Current.User.Id || Current.User.IsAdmin))
            {
                query.IsDeleted = false;
                Current.DB.SubmitChanges();
            }

            return Json("success");
        }

        [Route("saved_query/create", HttpVerbs.Post)]
        public ActionResult Create(SavedQuery query)
        {
            if (Current.User.IsAnonymous)
            {
                throw new InvalidOperationException("You must be logged on to access this method");
            }

            DBContext db = Current.DB;

            SavedQuery updateQuery = null;

            string oSavedQueryId = Request.Params["savedQueryId"];
            if (oSavedQueryId != null)
            {
                int savedQueryId;
                Int32.TryParse(oSavedQueryId, out savedQueryId);
                if (savedQueryId > 0)
                {
                    updateQuery = db.SavedQueries.FirstOrDefault(q => q.Id == savedQueryId);
                    if (updateQuery != null && (updateQuery.UserId == Current.User.Id || Current.User.IsAdmin))
                    {
                        updateQuery.QueryId = query.QueryId;
                        updateQuery.SiteId = query.SiteId;
                        updateQuery.Title = query.Title;
                        updateQuery.Description = query.Description;
                        updateQuery.LastEditDate = DateTime.UtcNow;
                    }
                    else
                    {
                        updateQuery = null;
                    }
                }
            }


            if (ModelState.IsValid)
            {
                if (updateQuery == null)
                {
                    query.CreationDate = query.LastEditDate = DateTime.UtcNow;
                    query.UserId = CurrentUser.Id;
                    db.SavedQueries.InsertOnSubmit(query);
                }
                db.SubmitChanges();
                return Json(new {success = true});
            }
            else
            {
                return Json(new {success = false, message = "Title must be at least 10 chars or longer!"});
            }
        }


        [HttpPost]
        [Route(@"feature_query/{id:\d+}")]
        public ActionResult Feature(int id, bool feature)
        {
            if (Current.User.IsAdmin)
            {
                Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id).IsFeatured = feature;
                Current.DB.SubmitChanges();
            }

            return Content("success");
        }

        [HttpPost]
        [Route(@"skip_query/{id:\d+}")]
        public ActionResult Skip(int id, bool skip)
        {
            if (Current.User.IsAdmin)
            {
                Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id).IsSkipped = skip;
                Current.DB.SubmitChanges();
            }

            return Content("success");
        }


        [Route("{sitename}/queries")]
        public ActionResult Index(string sitename, string order_by, string q, int? page, int? pagesize)
        {
            Site site = Current.DB.Sites.First(si => si.Name.ToLower() == sitename);

            QuerySearchCriteria searchCriteria = new QuerySearchCriteria(q);

            if (string.IsNullOrEmpty(order_by))
            {
                if (searchCriteria.IsValid)
                    order_by = searchCriteria.IsFeatured ? "featured" : "recent";
                else
                    order_by = "featured";
            }

            Site = site;
            SelectMenuItem("Queries");

            SetHeader("All Queries",
                      new SubHeaderViewData
                          {
                              Description = "featured",
                              Title = "Interesting queries selected by the administrators",
                              Href = "/" + sitename + "/queries?order_by=featured",
                              Selected = (order_by == "featured")
                          },
                      new SubHeaderViewData
                          {
                              Description = "recent",
                              Title = "Recently saved queries",
                              Href = "/" + sitename + "/queries?order_by=recent",
                              Selected = (order_by == "recent")
                          },
                      new SubHeaderViewData
                          {
                              Description = "favorite",
                              Title = "Favorite saved queries",
                              Href = "/" + sitename + "/queries?order_by=favorite",
                              Selected = (order_by == "favorite")
                          },
                      new SubHeaderViewData
                          {
                              Description = "popular",
                              Title = "Saved queries with the most views",
                              Href = "/" + sitename + "/queries?order_by=popular",
                              Selected = (order_by == "popular")
                          },
                      new SubHeaderViewData
                          {
                              Description = "everything",
                              Title = "All queries recently executed on the site",
                              Href = "/" + sitename + "/queries?order_by=everything",
                              Selected = (order_by == "everything")
                          }
                );


            ViewData["Site"] = site;

            DBContext db = Current.DB;

            var query = db.Queries.Select(qu => new {Query = qu, Saved = (SavedQuery) null});


            if (order_by != "everything")
            {
                query = from qu in query
                        join sq in db.SavedQueries on qu.Query.Id equals sq.QueryId
                        where sq.IsFirst
                        select new {qu.Query, Saved = sq};
            }

            if (order_by == "featured")
                query = query.Where(qu => qu.Saved.IsFeatured == true);

            if (order_by != "everything")
            {
                query = query.Where(qu => !(qu.Saved.IsDeleted ?? false));

                if (searchCriteria.IsValid)
                    query = query.Where(qu => (qu.Saved.Title.Contains(searchCriteria.SearchTerm) || qu.Saved.Description.Contains(searchCriteria.SearchTerm)));
            }

            ViewData["SearchCriteria"] = searchCriteria;



            IQueryable<QueryExecutionViewData> transformed =
                query.Select
                (
                    qu =>
                        new QueryExecutionViewData
                        {
                            Id = (qu.Saved == null) ? qu.Query.Id : qu.Saved.Id,
                            SQL = qu.Query.QueryBody,
                            Name = (qu.Saved == null) ? qu.Query.Name : qu.Saved.Title,
                            Description = (qu.Saved == null) ? qu.Query.Description : qu.Saved.Description,
                            SiteName = site.Name.ToLower(),
                            LastRun = qu.Query.FirstRun ?? DateTime.Now,
                            Views = qu.Query.Views ?? 1,
                            Creator = (qu.Saved == null) ? qu.Query.User : qu.Saved.User,
                            Featured = (qu.Saved == null) ? false : (qu.Saved.IsFeatured ?? false),
                            Skipped = (qu.Saved == null) ? false : (qu.Saved.IsSkipped ?? false),
                            FavoriteCount = (qu.Saved == null) ? 0 : qu.Saved.FavoriteCount,
                            UrlPrefix = (qu.Saved == null) ? "q" : "s"
                        }
                );

            if (order_by == "popular")
            {
                transformed = transformed.OrderByDescending(item => item.Views)
                    .ThenByDescending(item => item.FavoriteCount);
            }
            else if (order_by == "favorite" || order_by == "featured")
            {
                transformed = transformed.OrderByDescending(item => item.FavoriteCount)
                    .ThenByDescending(item => item.Views);
            }
            else
            {
                transformed = transformed.OrderByDescending(item => item.LastRun);
            }


            int totalQueries = transformed.Count();
            ViewData["TotalQueries"] = totalQueries;
            int currentPerPage = Math.Max(pagesize ?? 50, 1);
            int currentPage = Math.Max(page ?? 1, 1);
            string href = "/" + site.Name.ToLower() + "/queries?order_by=" + order_by;

            if (searchCriteria.IsValid)
                href += "&q=" + HtmlUtilities.UrlEncode(searchCriteria.RawInput);

            ViewData["PageNumbers"] = new PageNumber(href + "&page=-1", Convert.ToInt32(Math.Ceiling(totalQueries / (decimal)currentPerPage)), currentPerPage,
                                                     currentPage - 1, "pager");

            ViewData["PageSizer"] = new PageSizer(href + "&pagesize=-1", currentPage, currentPerPage, totalQueries,
                                                  "page-sizer fr");

            return View(transformed.Skip((currentPage - 1)*currentPerPage).Take(currentPerPage));
        }
    }
}