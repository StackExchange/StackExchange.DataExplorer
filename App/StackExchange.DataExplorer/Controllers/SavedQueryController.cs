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
        public ActionResult ShowLatest(string sitename, int ownerId, int rootId, string slug)
        {
            Site = GetSite(sitename);

            if (Site == null)
            {
                return PageNotFound();
            }

            var revision = QueryUtil.GetFeaturedCompleteRevision(ownerId, rootId);

            return ShowCommon(revision, slug, true);
        }

        [Route(@"{sitename}/query/{revisionId:\d+}/{slug?}")]
        public ActionResult Show(string sitename, int revisionId, string slug)
        {
            Site = GetSite(sitename);

            if (Site == null)
            {
                return PageNotFound();
            }

            var revision = QueryUtil.GetCompleteRevision(revisionId);

            return ShowCommon(revision, slug, false);
        }

        private ActionResult ShowCommon(Revision revision, string slug, bool latest)
        {
            if (revision == null)
            {
                return PageNotFound();
            }

            var title = revision.Metadata.Title;
            int rootId = revision.OwnerId != null ? revision.RootId.Value : revision.Id;
            int ownerId = revision.OwnerId ?? 0;

            SetHeader(title);
            SelectMenuItem("Queries");

            title = title.URLFriendly();

            // if this user has a display name, and the title is missing or does not match, permanently redirect to it
            if (title.HasValue() && (string.IsNullOrEmpty(slug) || slug != title))
            {
                string url = "/{0}/query/{2}/{3}";

                if (latest)
                {
                    url = "/{0}/query/{1}/{2}/{3}";
                }

                return PageMovedPermanentlyTo(string.Format(url, new object[] {
                    Site.Name.ToLower(),
                    ownerId,
                    rootId,
                    title
                }) + Request.Url.Query);
            }

            ViewData["GuessedUserId"] = Site.GuessUserId(CurrentUser);

            // Need to revamp voting process
            int totalVotes = Current.DB.Query<int>(@"
                SELECT
                    COUNT(*)
                FROM
                    Votes
                WHERE
                    RootId = @root AND
                    VoteTypeId = @voteType AND
                    OwnerId " + (ownerId > 0 ? "= @owner" : "IS NULL"),
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
                        VoteTypeId = @voteType AND
                        UserId = @user AND
                        OwnerId " + (ownerId > 0 ? "= @owner" : "IS NULL"),
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
            ViewData["query_action"] = "run/" + Site.Id + "/" + revision.Id;

            if (!IsSearchEngine())
            {
                QueryViewTracker.TrackQueryView(GetRemoteIP(), rootId, ownerId);
            }

            return View("Viewer", revision);
        }

        [HttpPost]
        [Route(@"feature_query/{id:\d+}")]
        public ActionResult Feature(int id, bool feature)
        {
            if (Current.User.IsAdmin)
            {
                Revision revision = QueryUtil.GetCompleteRevision(id);

                if (revision != null)
                {
                    Current.DB.Execute("UPDATE Metadata SET Featured = 1 WHERE Id = @id", new { id = revision.Metadata.Id });
                }
            }

            return Content("success");
        }

        [HttpPost]
        [Route(@"skip_query/{id:\d+}")]
        public ActionResult Skip(int id, bool skip)
        {
            if (Current.User.IsAdmin)
            {
                /*
                Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id).IsSkipped = skip;
                Current.DB.SubmitChanges();
                 */
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

            QuerySearchCriteria searchCriteria = new QuerySearchCriteria(q);

            if (string.IsNullOrEmpty(order_by))
            {
                if (searchCriteria.IsValid)
                    order_by = searchCriteria.IsFeatured ? "featured" : "recent";
                else
                    order_by = "featured";
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

            IEnumerable<QueryExecutionViewData> queries;

            if (order_by == "recent")
            {
                queries = Current.DB.Query<Metadata, Query, User, QueryExecutionViewData>(@"
                    SELECT
                        metadata.*, query.*, [user].*
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
                    JOIN
                        Queries query
                    ON
                        query.Id = metadata.LastQueryId
                    LEFT OUTER JOIN
                        Users [user]
                    ON
                        metadata.OwnerId = [user].Id
                    ORDER BY
                        metadata.LastActivity DESC",
                    (metadata, query, user) =>
                    {
                        return new QueryExecutionViewData
                        {
                            Id = metadata.RevisionId,
                            Name = metadata.Title,
                            DefaultName = query.AsTitle(),
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
                queries = Current.DB.Query<Revision, Query, Metadata, User, QueryExecutionViewData>(@"
                    SELECT
                        *
                    FROM
                        Revisions revision
                    JOIN
                        Queries query
                    ON
                        query.Id = revision.QueryId
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
                    (revision, query, metadata, user) =>
                    {
                        return new QueryExecutionViewData
                        {
                            Id = revision.Id,
                            Name = metadata.Title,
                            DefaultName = query.AsTitle(),
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

                queries = Current.DB.Query<Metadata, Query, User, QueryExecutionViewData>(String.Format(@"
                    SELECT
                        *
                    FROM
                        Metadata metadata
                    JOIN
                        Queries query
                    ON
                        query.Id = metadata.LastQueryId
                    LEFT OUTER JOIN
                        Users [user]
                    ON
                        metadata.OwnerId = [user].Id
                    WHERE
                        metadata.{0} > @threshold
                    ORDER BY
                        metadata.{0} DESC,
                        metadata.{1} DESC", primary, fallback),
                    (metadata, query, user) => {
                        return new QueryExecutionViewData {
                            Id = metadata.RevisionId,
                            Name = metadata.Title,
                            DefaultName = query.AsTitle(),
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