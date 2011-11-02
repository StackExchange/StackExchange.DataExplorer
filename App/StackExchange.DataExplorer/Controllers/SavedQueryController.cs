using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using Dapper.Contrib.Extensions;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;


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
                    latest ? rootId : revision.Id,
                    title
                }) + Request.Url.Query);
            }

            if (title.IsNullOrEmpty())
            {
                title = revision.Query.AsTitle();
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
                TotalVotes = totalVotes,
                RevisionId = revision.Id,
                ReadOnly = revision.OwnerId == CurrentUser.Id
            };

            if (!Current.User.IsAnonymous && revision.OwnerId != CurrentUser.Id)
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
            ViewData["query_action"] = "run/" + Site.Id + "/" + revision.Id;

            if (cachedResults != null)
            {
                ViewData["cached_results"] = new QueryResults
                {
                    RevisionId = revision.Id,
                    SiteId = Site.Id,
                    SiteName = Site.Name,
                    Slug = revision.Metadata.Title.URLFriendly()
                }.WithCache(cachedResults);
            }

            if (!IsSearchEngine())
            {
                QueryViewTracker.TrackQueryView(GetRemoteIP(), rootId, ownerId);
            }

            var viewmodel = new QueryExecutionViewData
            {
                QueryVoting = voting,
                Id = revision.Id,
                Name = revision.Metadata.Title,
                Description = revision.Metadata.Description,
                FavoriteCount = revision.Metadata.Votes,
                Views = revision.Metadata.Views,
                LastRun = revision.Metadata.LastActivity,
                CreatorId = revision.Owner != null ? revision.Owner.Id : (int?)null,
                CreatorLogin = revision.Owner != null ? revision.Owner.Login : null,
                SiteName = Site.Name.ToLower(),
                SQL = revision.Query.QueryBody,
                UseLatestLink = false
            };

            return View("Viewer", viewmodel);
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

            pagesize = Math.Max(Math.Min(pagesize ?? 50, 100), 10);
            page = Math.Max(page ?? 1, 1);

            int start = ((page.Value - 1) * pagesize.Value) + 1;
            int finish = page.Value * pagesize.Value;
            bool useLatest = true;
            var builder = new SqlBuilder();
            var pager = builder.AddTemplate(@"
                SELECT
                    *
                FROM    
                    (
                        SELECT
                            /**select**/, ROW_NUMBER() OVER(/**orderby**/) AS RowNumber
                        FROM
                            Metadata metadata
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
            var counter = builder.AddTemplate("SELECT COUNT(*) FROM Metadata metadata /**join**/ /**leftjoin**/ /**where**/");

            if (order_by != "everything")
            {
                builder.Select("metadata.RevisionId AS Id");
                builder.Select("metadata.LastActivity AS LastRun");
                builder.Join("Queries query ON query.Id = metadata.LastQueryId");
                builder.LeftJoin("Users [user] ON metadata.OwnerId = [user].Id");

                if (order_by == "featured" || order_by == "recent")
                {
                    if (order_by == "featured")
                    {
                        builder.Where("metadata.Featured = 1");
                    }

                    builder.OrderBy("metadata.LastActivity DESC");
                }
                else
                {
                    int threshold = 0;

                    if (order_by == "popular")
                    {
                        builder.Where("metadata.Views > @threshold", new { threshold = threshold });
                        builder.OrderBy("metadata.Views DESC");
                        builder.OrderBy("metadata.Votes DESC");
                    }
                    else
                    {
                        builder.Where("metadata.Votes > @threshold", new { threshold = threshold });
                        builder.OrderBy("metadata.Votes DESC");
                        builder.OrderBy("metadata.Views DESC");
                    }
                }
            }
            else if (order_by == "everything")
            {
                builder.Select("revision.Id AS Id");
                builder.Select("revision.CreationDate AS LastRun");
                builder.Join(@"
                    Revisions revision ON 
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
                    )"
                );
                builder.Join("Queries query on query.Id = revision.QueryId");
                builder.LeftJoin("Users [user] ON revision.OwnerId = [user].Id");
                builder.OrderBy("revision.CreationDate DESC");

                useLatest = false;
            }

            builder.Select("[user].Id as CreatorId");
            builder.Select("[user].Login as CreatorLogin");
            builder.Select("metadata.Title AS Name");
            builder.Select("metadata.[Description] AS [Description]");
            builder.Select("metadata.Votes AS FavoriteCount");
            builder.Select("metadata.Views AS Views");
            builder.Select("query.QueryBody AS [SQL]");

            if (searchCriteria.IsValid)
            {
                builder.Where("metadata.Title LIKE @search OR metadata.[Description] LIKE @search", new { search = '%' + searchCriteria.SearchTerm + '%' });
            }

            IEnumerable<QueryExecutionViewData> queries = Current.DB.Query<QueryExecutionViewData>(
                pager.RawSql,
                pager.Parameters
            ).Select<QueryExecutionViewData, QueryExecutionViewData>(
                (view) =>
                {
                    view.UseLatestLink = useLatest;
                    view.SiteName = Site.Name.ToLower();

                    return view;
                }
            );
            int total = Current.DB.Query<int>(counter.RawSql, counter.Parameters).First();
            
            string href = "/" + Site.Name.ToLower() + "/queries?order_by=" + order_by;

            if (searchCriteria.IsValid)
            {
                href += "&q=" + HtmlUtilities.UrlEncode(searchCriteria.RawInput);
            }

            ViewData["SearchCriteria"] = searchCriteria;
            ViewData["TotalQueries"] = total;
            ViewData["PageNumbers"] = new PageNumber(
                href + "&page=-1",
                Convert.ToInt32(Math.Ceiling(total / (decimal)pagesize)),
                pagesize.Value,
                page.Value - 1,
                "pager"
            );
            ViewData["PageSizer"] = new PageSizer(
                href + "&pagesize=-1",
                page.Value,
                pagesize.Value,
                total,
                "page-sizer fr"
            );

            return View(queries);
        }
    }
}