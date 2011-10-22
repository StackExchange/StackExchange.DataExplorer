using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;
using System.Collections.Generic;

namespace StackExchange.DataExplorer.Controllers
{
    public class SavedQueryController : StackOverflowController
    {
        [Route(@"{sitename}/query/{ownerId:\d+}/{rootId:\d+}/{slug?}")]
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
                QueryViewTracker.TrackQueryView(GetRemoteIP(), rootId, ownerId);
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
            Site = GetSite(sitename);

            if (Site == null)
            {
                return PageNotFound();
            }

            ViewData["Site"] = Site;
            SelectMenuItem("Queries");
            SetHeader(
                "All Queries",
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

            QuerySearchCriteria searchCriteria = new QuerySearchCriteria(q);

            if (string.IsNullOrEmpty(order_by))
            {
                if (searchCriteria.IsValid)
                    order_by = searchCriteria.IsFeatured ? "featured" : "recent";
                else
                    order_by = "featured";
            }

            IEnumerable<QueryExecutionViewData> queries;

            if (order_by == "recent")
            {
                queries = Current.DB.Query<Metadata, User, QueryExecutionViewData>(@"
                    SELECT
                        metadata.*, [user].*
                    FROM
                        (
                            SELECT
	                            MAX(metadata.Id) AS Id
                            FROM
	                            Metadata metadata
                            JOIN
	                            Revisions revisions
                            ON
	                            metadata.RevisionId = revisions.Id
                            GROUP BY ISNULL(revisions.RootId, revisions.Id)
                        ) ids
                    JOIN
                        Metadata metadata
                    ON
                        metadata.id = ids.id
                    LEFT OUTER JOIN
                        Users [user]
                    ON
                        metadata.OwnerId = [user].Id
                    ORDER BY
                        metadata.LastActivity DESC",
                    (metadata, user) =>
                    {
                        return new QueryExecutionViewData
                        {
                            Id = metadata.RevisionId,
                            Name = metadata.Title,
                            Description = metadata.Description,
                            FavoriteCount = metadata.Votes,
                            Views = metadata.Views,
                            LastRun = metadata.LastActivity,
                            Creator = user,
                            SiteName = Site.Name.ToLower(),
                            UseLatestLink = true
                        };
                    }
                );
            }
            else if (order_by == "everything")
            {
                queries = Current.DB.Query<Revision, Metadata, User, QueryExecutionViewData>(@"
                    SELECT
                        *
                    FROM
                        Revisions revision
                    JOIN
                        Metadata metadata
                    ON 
                        (
                            metadata.RevisionId = revision.RootId AND
                            metadata.OwnerId = revision.OwnerId
                        ) OR (
                            metadata.RevisionId = revision.Id AND
                            metadata.OwnerId = revision.OwnerId AND
                            revision.RootId IS NULL
                        ) OR (
                            metadata.RevisionId = revision.Id AND
                            metadata.OwnerId IS NULL AND
                            revision.OwnerId IS NULL
                        )
                    LEFT OUTER JOIN
                        Users [user]
                    ON
                        revision.OwnerId = [user].Id
                    ORDER BY
                        revision.CreationDate DESC",
                    (revision, metadata, user) =>
                    {
                        return new QueryExecutionViewData
                        {
                            Id = revision.Id,
                            Name = metadata.Title,
                            Description = metadata.Description,
                            FavoriteCount = metadata.Votes,
                            Views = metadata.Views,
                            LastRun = metadata.LastActivity,
                            Creator = user,
                            SiteName = Site.Name.ToLower(),
                            UseLatestLink = false
                        };
                    }
                );
            }
            else
            {
                // The default view is favorite
                int threshold = 0;
                string primary = "Votes";
                string fallback = "Views";

                if (order_by == "popular") {
                    primary = "Views";
                    fallback = "Votes";
                }

                queries = Current.DB.Query<Metadata, User, QueryExecutionViewData>(String.Format(@"
                    SELECT
                        *
                    FROM
                        Metadata metadata
                    LEFT OUTER JOIN
                        Users [user]
                    ON
                        metadata.OwnerId = [user].Id
                    WHERE
                        metadata.{0} > @threshold
                    ORDER BY
                        metadata.{0} DESC,
                        metadata.{1} DESC", primary, fallback),
                    (metadata, user) => {
                        return new QueryExecutionViewData {
                            Id = metadata.RevisionId,
                            Name = metadata.Title,
                            Description = metadata.Description,
                            FavoriteCount = metadata.Votes,
                            Views = metadata.Views,
                            LastRun = metadata.LastActivity,
                            Creator = user,
                            SiteName = Site.Name.ToLower(),
                            UseLatestLink = true
                        };
                    },
                    new
                    {
                        threshold = threshold
                    }
                );
            }

            int totalQueries = queries.Count();
            int currentPerPage = Math.Max(pagesize ?? 50, 1);
            int currentPage = Math.Max(page ?? 1, 1);
            string href = "/" + Site.Name.ToLower() + "/queries?order_by=" + order_by;

            if (searchCriteria.IsValid)
            {
                href += "&q=" + HtmlUtilities.UrlEncode(searchCriteria.RawInput);
            }

            ViewData["SearchCriteria"] = searchCriteria;
            ViewData["TotalQueries"] = totalQueries;
            ViewData["PageNumbers"] = new PageNumber(
                href + "&page=-1",
                Convert.ToInt32(Math.Ceiling(totalQueries / (decimal)currentPerPage)),
                currentPerPage,
                currentPage - 1,
                "pager"
            );
            ViewData["PageSizer"] = new PageSizer(
                href + "&pagesize=-1",
                currentPage,
                currentPerPage,
                totalQueries,
                "page-sizer fr"
            );

            return View(queries.Skip((currentPage - 1) * currentPerPage).Take(currentPerPage));
        }
    }
}