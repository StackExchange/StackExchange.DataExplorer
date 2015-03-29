using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

using Dapper;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;


namespace StackExchange.DataExplorer.Controllers
{
    public class QuerySetController : StackOverflowController
    {
        [StackRoute(@"{sitename}/s/{queryId:\d+}/{slug?}")]
        public ActionResult MapQuery(string sitename, int queryId, string slug)
        {
            Revision revision = QueryUtil.GetMigratedRevision(queryId, MigrationType.Saved);

            if (revision == null)
            {
                return PageNotFound();
            }

            if (slug.HasValue())
            {
                slug = "/" + slug;
            }

            return new RedirectPermanentResult("/" + sitename + "/query/" + revision.QuerySet.Id + slug);
        }

        [StackRoute(@"{sitename}/query/{querySetId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowLatest(string sitename, int querySetId, string slug)
        {
            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/query/{1}{2}{3}",
                    site.TinyName.ToLower(), querySetId, slug.HasValue() ? "/" + slug : "", Request.Url.Query
                ));
            }

            Site = site;
            var querySet = QueryUtil.GetFullQuerySet(querySetId);

            if (querySet == null)
            {
                return PageNotFound();
            }
            
            var revision = querySet.CurrentRevision;

            return ShowCommon(revision, slug, true);
        }

        [StackRoute(@"{sitename}/revision/{querySetId:\d+}/{revisionId:\d+}/{slug?}")]
        public ActionResult Show(string sitename, int querySetId, int revisionId, string slug)
        {
            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/revision/{1}/{2}{3}{4}",
                    site.TinyName.ToLower(), querySetId, revisionId, slug.HasValue() ? "/" + slug : "", Request.Url.Query
                ));
            }

            Site = site;
            var querySet = QueryUtil.GetFullQuerySet(querySetId);

            if (querySet == null)
            {
                return PageNotFound();
            }

            var revision = querySet.Revisions.FirstOrDefault(r => r.Id == revisionId);

            if (revision == null)
            {
                return PageNotFound();
            }

            return ShowCommon(revision, slug, false);
        }

        private ActionResult ShowCommon(Revision revision, string slug, bool latest)
        {
            if (revision == null)
            {
                return PageNotFound();
            }

            var title = revision.QuerySet.Title;
            int ownerId = revision.OwnerId ?? 0;

            title = title.URLFriendly();

            // if this query has a title, and the title is missing or does not match, permanently redirect to it
            if (title.HasValue() && (string.IsNullOrEmpty(slug) || slug != title))
            {
                string url = latest ? "/{0}/query/{1}/{3}" : "/{0}/revision/{1}/{2}/{3}";

                return PageMovedPermanentlyTo(string.Format(url,
                    Site.TinyName.ToLower(),
                    revision.QuerySet.Id,
                    revision.Id,
                    title
                ) + Request.Url.Query);
            }

            title = revision.QuerySet.Title;

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
                    QuerySetId = @querySetId AND
                    VoteTypeId = @voteType",
                new
                {
                    querySetId = revision.QuerySet.Id,
                    voteType = (int)VoteType.Favorite
                }
            ).FirstOrDefault();

            var voting = new QuerySetVoting
            {
                TotalVotes = totalVotes,
                QuerySetId = revision.QuerySet.Id,
                ReadOnly = CurrentUser.IsAnonymous
            };

            if (!Current.User.IsAnonymous)
            {
                voting.HasVoted = Current.DB.Query<Vote>(@"
                    SELECT
                        *
                    FROM
                        Votes
                    WHERE
                        QuerySetId = @querySetId AND
                        VoteTypeId = @voteType AND
                        UserId = @user",
                   new
                   {
                       querySetId = revision.QuerySet.Id,
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
            ViewData["query_action"] = "run/" + Site.Id + "/" + revision.QuerySet.Id + "/" + revision.Id;

            if (cachedResults != null)
            {
                // Don't show cached execution plan, since the user didn't ask for it...
                cachedResults.ExecutionPlan = null;

                ViewData["cached_results"] = new QueryResults
                {
                    RevisionId = revision.Id,
                    QuerySetId = revision.QuerySet.Id,
                    SiteId = Site.Id,
                    SiteName = Site.TinyName.ToLower(),
                    Slug = revision.QuerySet.Title.URLFriendly(),
                    Url = Site.Url
                }.WithCache(cachedResults);
            }

            if (!IsSearchEngine())
            {
                QueryViewTracker.TrackQueryView(GetRemoteIP(), revision.QuerySet.Id);
            }

            var initialRevision = revision.QuerySet.InitialRevision; 

            var viewmodel = new QueryViewerData
            {
                QuerySetVoting = voting,
                Revision = revision
            };

            return View("Viewer", viewmodel);
        }

        [HttpPost]
        [StackRoute(@"feature_query/{querySetId:\d+}")]
        public ActionResult Feature(int querySetId, bool feature)
        {
            if (Current.User.IsAdmin)
            {
                var querySet = Current.DB.QuerySets.Get(querySetId);

                if (querySet != null)
                {
                    Current.DB.QuerySets.Update(querySetId, new { Featured = true });
                }
            }

            return Content("success");
        }

        [HttpPost]
        [StackRoute(@"skip_query/{id:\d+}")]
        public ActionResult Skip(int id, bool skip)
        {
            if (Current.User.IsAdmin)
            {
                // do it
            }

            return Content("failed");
        }


        [StackRoute("{sitename}", Priority = RoutePriority.Low)]
        public ActionResult IndexFallback(string sitename)
        {
            Site site;
            TryGetSite(sitename, out site);

            return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/queries",
                site.TinyName.ToLower()
            ));
        }

        [StackRoute("{sitename}/queries")]
        public ActionResult Index(string sitename, string order_by, string q, int? page, int? pagesize)
        {
            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/queries",
                    site.TinyName.ToLower()
                ));
            }

            Site = site;
            QuerySearchCriteria searchCriteria = new QuerySearchCriteria(q);

            if (string.IsNullOrEmpty(order_by))
            {
                if (searchCriteria.IsValid)
                {
                    order_by = searchCriteria.IsFeatured ? "featured" : "recent";
                }
                else
                {
                    order_by = CurrentUser.DefaultQuerySort ?? "featured";
                }
            }

            if (!searchCriteria.IsValid)
            {
                CurrentUser.DefaultQuerySort = order_by;
            }

            ViewData["Site"] = Site;
            SelectMenuItem("Queries");

            var pagesizeProvided = pagesize.HasValue;

            if (!Current.User.IsAnonymous && !pagesizeProvided)
            {
                pagesize = Current.User.DefaultQueryPageSize;
            }

            pagesize = Math.Max(Math.Min(pagesize ?? 50, 100), 10);
            page = Math.Max(page ?? 1, 1);

            if (!Current.User.IsAnonymous)
            {
                Current.User.DefaultQueryPageSize = pagesize;
            }

            int start = ((page.Value - 1) * pagesize.Value) + 1;
            int finish = page.Value * pagesize.Value;
            var builder = new SqlBuilder();
            SqlBuilder.Template pager = null, counter = null;

            if (order_by != "everything")
            {
                pager = builder.AddTemplate(@"
                    SELECT
                        *
                    FROM    
                        (
                            SELECT
                                /**select**/, ROW_NUMBER() OVER(/**orderby**/) AS RowNumber
                            FROM
                                QuerySets qs
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
                counter = builder.AddTemplate("SELECT COUNT(*) FROM QuerySets qs /**join**/ /**leftjoin**/ /**where**/");

                builder.Select("qs.Id as QuerySetId");
                builder.Select("qs.LastActivity AS LastRun");
                builder.Join("Revisions r ON r.Id = qs.CurrentRevisionId");
                builder.Join("Queries q ON q.Id = r.QueryId");
                builder.LeftJoin("Users u ON qs.OwnerId = u.Id");
                builder.Where("qs.Hidden = 0");
                builder.Where("qs.Title is not null");
                builder.Where("qs.Title <> ''");

                if (order_by == "featured" || order_by == "recent")
                {
                    if (order_by == "featured")
                    {
                        builder.Where("qs.Featured = 1");
                        builder.OrderBy("qs.Votes DESC");
                    }

                    builder.OrderBy("qs.LastActivity DESC");
                }
                else
                {
                    int threshold = 0;

                    if (order_by == "popular")
                    {
                        builder.Where("qs.Views > @threshold", new { threshold = threshold });
                        builder.OrderBy("qs.Views DESC");
                        builder.OrderBy("qs.Votes DESC");
                    }
                    else
                    {
                        order_by = "favorite";
                        builder.Where("qs.Votes > @threshold", new { threshold = threshold });
                        builder.OrderBy("qs.Votes DESC");
                        builder.OrderBy("qs.Views DESC");
                    }
                }

                if (searchCriteria.IsValid)
                {
                    builder.Where("qs.Title LIKE @search OR qs.[Description] LIKE @search", new { search = '%' + searchCriteria.SearchTerm + '%' });
                }
            }
            else if (order_by == "everything")
            {
                pager = builder.AddTemplate(@"
                    SELECT
                        /**select**/
                    FROM (
                        SELECT r.*, ROW_NUMBER() OVER(/**orderby**/) AS RowNumber FROM Revisions r
                    ) r
                    /**join**/
                    /**leftjoin**/
                    WHERE 
                        RowNumber BETWEEN @start AND @finish
                    ORDER BY
                        RowNumber",
                    new { start = start, finish = finish }
                );
                counter = builder.AddTemplate("SELECT COUNT(*) FROM Revisions r");

                builder.Select("r.Id AS RevisionId");
                builder.Select("qs.Id as QuerySetId");
                builder.Select("r.CreationDate AS LastRun");
                builder.Join("QuerySetRevisions qr on qr.RevisionId = r.Id ");
                builder.Join("QuerySets qs on qs.Id = qr.QuerySetId");
                builder.Join("Queries q on q.Id = r.QueryId");
                builder.LeftJoin("Users u ON r.OwnerId = u.Id");
                builder.OrderBy("CreationDate DESC");
            }

            builder.Select("u.Id as CreatorId");
            builder.Select("u.Login as CreatorLogin");
            builder.Select("qs.Title AS Name");
            builder.Select("qs.[Description] AS [Description]");
            builder.Select("qs.Votes AS FavoriteCount");
            builder.Select("qs.Views AS Views");
            builder.Select("q.QueryBody AS [SQL]");

            IEnumerable<QueryExecutionViewData> queries = Current.DB.Query<QueryExecutionViewData>(
                pager.RawSql,
                pager.Parameters
            ).Select(
                (view) =>
                {
                    view.SiteName = Site.TinyName.ToLower();
                    return view;
                }
            );
            int total = Current.DB.Query<int>(counter.RawSql, counter.Parameters).First();

            sitename = Site.TinyName.ToLower();
            
            var href = "/" + sitename + "/queries?order_by={0}";
            var extra = "";

            if (searchCriteria.IsValid)
            {
                extra += "&q=" + HtmlUtilities.UrlEncode(searchCriteria.RawInput);
            }

            // TODO: This is a hack to avoid misrepresenting the fact we aren't actually doing the search in this case
            if (order_by == "everything")
            {
                searchCriteria = new QuerySearchCriteria(null);
            }

            ViewData["SearchCriteria"] = searchCriteria;
            ViewData["Href"] = string.Format(href, order_by) + extra;

            if (Current.User.IsAnonymous && pagesizeProvided)
            {
                extra += "&pagesize=" + pagesize.Value;
            }

            SetHeader(
                "All Queries",
                new SubHeaderViewData
                {
                    Description = "featured",
                    Title = "Interesting queries selected by the administrators",
                    Href = string.Format(href, "featured") + extra,
                    Selected = (order_by == "featured")
                },
                new SubHeaderViewData
                {
                    Description = "recent",
                    Title = "Recently saved queries",
                    Href = string.Format(href, "recent") + extra,
                    Selected = (order_by == "recent")
                },
                new SubHeaderViewData
                {
                    Description = "favorite",
                    Title = "Favorite saved queries",
                    Href = string.Format(href, "favorite") + extra,
                    Selected = (order_by == "favorite")
                },
                new SubHeaderViewData
                {
                    Description = "popular",
                    Title = "Saved queries with the most views",
                    Href = string.Format(href, "popular") + extra,
                    Selected = (order_by == "popular")
                },
                new SubHeaderViewData
                {
                    Description = "everything",
                    Title = "All queries recently executed on the site",
                    Href = string.Format(href, "everything") + extra,
                    Selected = (order_by == "everything")
                }
            );

            return View(new PagedList<QueryExecutionViewData>(queries, page.Value, pagesize.Value, false, total));
        }
    }
}