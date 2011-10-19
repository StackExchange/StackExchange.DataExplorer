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
        [Route(@"{sitename}/query/show/{ownerId:\d+}/{rootId:\d+}/{slug?}")]
        public ActionResult Show(string sitename, int ownerId, int rootId, string slug)
        {
            Site = GetSite(sitename);

            if (Site == null)
            {
                return PageNotFound();
            }

            var revision = QueryUtil.GetFeaturedCompleteRevision(ownerId, rootId);
            
            if (revision == null)
            {
                return PageNotFound();
            }

            var title = revision.Metadata.Title;

            // if this user has a display name, and the title is missing or does not match, permanently redirect to it
            if (title.URLFriendly().HasValue() && (string.IsNullOrEmpty(slug) || slug != title.URLFriendly()))
            {
                return PageMovedPermanentlyTo(string.Format("/{0}/query/show/{1}/{2}", sitename, rootId, title.URLFriendly()) + Request.Url.Query);
            }

            SetHeader(title);
            SelectMenuItem("Queries");
            ViewData["GuessedUserId"] = Site.GuessUserId(CurrentUser);

            // Need to revamp voting process
            int totalVotes = Current.DB.Query<int>(@"
                SELECT
                    COUNT(*)
                FROM
                    Votes
                WHERE
                    RootId = @root AND
                    OwnerId = @owner AND
                    VoteTypeId = @voteType",
                new
                {
                    root = rootId,
                    owner = ownerId,
                    voteType = (int)VoteType.Favorite
                }
            ).FirstOrDefault();

            var voting = new QueryVoting
            {
                TotalVotes = totalVotes
            };

            if (!Current.User.IsAnonymous)
            {
                voting.HasVoted = Current.DB.Query<Vote>(@"
                    SELECT
                        *
                    FROM
                        Votes
                    WHERE
                        RootId = @root AND
                        OwnerId = @owner AND
                        VoteTypeId = @voteType AND
                        UserId = @user",
                   new
                   {
                       root = rootId,
                       owner = ownerId,
                       voteType = (int)VoteType.Favorite,
                       user = Current.User.Id
                   }
               ).FirstOrDefault() != null;
            }

            CachedResult cachedResults = QueryUtil.GetCachedResults(
                new ParsedQuery(revision.Query.QueryBody, Request.Params),
                Site.Id
            );

            ViewData["QueryVoting"] = voting;
            ViewData["Sites"] = Current.DB.Sites.ToList();
            ViewData["cached_results"] = cachedResults;

            if (!IsSearchEngine())
            {
                // View tracking needs to be updated in line with vote tracking
                //QueryViewTracker.TrackQueryView(GetRemoteIP(), savedQuery.Query.Id);
            }

            return View(revision);
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
            Site site = GetSite(sitename);
            if (site==null)
            {
                return PageNotFound();
            }

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