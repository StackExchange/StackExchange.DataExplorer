using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using Newtonsoft.Json;

namespace StackExchange.DataExplorer.Controllers
{
    public class QueryController : StackOverflowController
    {
        [StackRoute(@"query/job/{guid}")]
        public ActionResult PollJob(Guid guid)
        {
            var result = AsyncQueryRunner.PollJob(guid);
            if (result == null)
            {
                return Json(new {error = "unknown job being polled!" });
            }

            if (result.State == AsyncQueryRunner.AsyncState.Failure)
            {
                return TransformExecutionException(result.Exception);
            }

            if (result.State == AsyncQueryRunner.AsyncState.Pending)
            {
                return Json(new { running = true, job_id = result.JobId });
            }

            try
            {
                return CompleteResponse(result.QueryResults, result.ParsedQuery, result.QueryContextData, result.Site.Id);
            }
            catch (Exception ex)
            { 
                return TransformExecutionException(ex);
            }

        }

        [HttpPost]
        [StackRoute(@"query/job/{guid}/cancel")]
        public ActionResult CancelJob(Guid guid)
        {
            if (!AppSettings.EnableCancelQuery)
            {
                throw new ApplicationException("Cancelling queries is not enabled");
            }

            var result = AsyncQueryRunner.CancelJob(guid);

            if (result == null)
            {
                return Json(new { error = "can't cancel unknown job!" });
            }

            return Json(new { cancelled = !result.HasOutput, job_id = result.JobId });
        }

        [HttpPost]
        [StackRoute(@"query/save/{siteId:\d+}/{querySetId?:\d+}")]
        public ActionResult Save(string sql, string title, string description, int siteId, int? querySetId, bool? textResults, bool? withExecutionPlan, bool? bypassCache, TargetSites? targetSites)
        {
            if (CurrentUser.IsAnonymous && !CaptchaController.CaptchaPassed(GetRemoteIP()))
            {
                return Json(new { captcha = true });
            }

            ActionResult response = null;
            try
            {
                if (!ValidateTargetSites(targetSites))
                {
                    throw new ApplicationException("Invalid target sites selection");
                }

                QuerySet querySet = null;

                if (querySetId.HasValue)
                {
                    querySet = Current.DB.QuerySets.Get(querySetId.Value);

                    if (querySet == null)
                    {
                        throw new ApplicationException("Invalid query set ID");
                    }
                }

                var parsedQuery = new ParsedQuery(
                    sql,
                    Request.Params,
                    withExecutionPlan == true,
                    targetSites ?? TargetSites.Current
                );

                if (AppSettings.EnableBypassCache && bypassCache.HasValue && bypassCache.Value)
                {
                    QueryUtil.ClearCachedResults(parsedQuery, siteId);
                }

                QueryResults results = null;
                Site site = GetSite(siteId);
                ValidateQuery(parsedQuery, site);

                if (title.HasValue() && title.Length > 100)
                {
                    throw new ApplicationException("Title must be no more than 100 characters");
                }

                if (description.HasValue() && description.Length > 1000)
                {
                    throw new ApplicationException("Description must be no more than 1000 characters");
                }

                var contextData = new QueryContextData 
                { 
                    Title = title,
                    Description = description,
                    IsText = textResults == true,
                    QuerySet = querySet
                };

                var asyncResults = AsyncQueryRunner.Execute(parsedQuery, CurrentUser, site, contextData);

                if (asyncResults.State == AsyncQueryRunner.AsyncState.Failure)
                {
                    throw asyncResults.Exception; 
                }

                if (asyncResults.State == AsyncQueryRunner.AsyncState.Success || asyncResults.State == AsyncQueryRunner.AsyncState.Cancelled)
                {
                    results = asyncResults.QueryResults;
                }
                else
                {
                    return Json(new {running = true, job_id = asyncResults.JobId});
                }

                response = CompleteResponse(results, parsedQuery, contextData, siteId);
            }
            catch (Exception ex)
            {
                response = TransformExecutionException(ex);
            }

            return response;
        }

        private ActionResult CompleteResponse(
            QueryResults results, 
            ParsedQuery parsedQuery, 
            QueryContextData context,
            int siteId
            )
        {
            results = TranslateResults(parsedQuery, context.IsText, results);

            var query = Current.DB.Query<Query>(
                "SELECT * FROM Queries WHERE QueryHash = @hash",
                new
                {
                    hash = parsedQuery.Hash
                }
            ).FirstOrDefault();

            int revisionId = 0;
            DateTime saveTime;

            // We only create revisions if something actually changed.
            // We'll log it as an execution anyway if applicable, so the user will
            // still get a link in their profile, just not their own revision.
            if (context.Revision == null && (context.QuerySet == null || query == null || context.QuerySet.CurrentRevision == null || context.QuerySet.CurrentRevision.QueryId != query.Id))
            {
                int queryId; 
                if (query == null)
                {
                    queryId = (int)Current.DB.Queries.Insert(
                        new
                        {
                            QueryHash = parsedQuery.Hash,
                            QueryBody = parsedQuery.Sql
                        }
                    );
                }
                else
                {
                    queryId = query.Id;
                }

                revisionId = (int)Current.DB.Revisions.Insert(
                    new
                    {
                        QueryId = queryId,
                        OwnerId = CurrentUser.IsAnonymous ? null : (int?)CurrentUser.Id,
                        OwnerIP = GetRemoteIP(),
                        CreationDate = saveTime = DateTime.UtcNow,
                        OriginalQuerySetId = context.QuerySet != null ? context.QuerySet.Id : (int?)null
                    }
                );

                int querySetId;
                // brand new queryset 
                if (context.QuerySet == null)
                { 
                    // insert it 
                    querySetId = (int)Current.DB.QuerySets.Insert(new 
                    {
                        InitialRevisionId = revisionId,
                        CurrentRevisionId = revisionId, 
                        context.Title,
                        context.Description,
                        LastActivity = DateTime.UtcNow,
                        Votes = 0,
                        Views = 0,
                        Featured = false,
                        Hidden = false,
                        CreationDate = DateTime.UtcNow, 
                        OwnerIp = CurrentUser.IPAddress,
                        OwnerId = CurrentUser.IsAnonymous?(int?)null:CurrentUser.Id
                    });

                    Current.DB.Revisions.Update(revisionId, new { OriginalQuerySetId = querySetId });
                }
                else if (
                    (CurrentUser.IsAnonymous && context.QuerySet.OwnerIp == CurrentUser.IPAddress) || context.QuerySet.OwnerId != CurrentUser.Id)
                {
                    // fork it 
                    querySetId = (int)Current.DB.QuerySets.Insert(new
                    {
                        InitialRevisionId = context.QuerySet.InitialRevisionId,
                        CurrentRevisionId = revisionId,
                        context.Title,
                        context.Description,
                        LastActivity = DateTime.UtcNow,
                        Votes = 0,
                        Views = 0,
                        Featured = false,
                        Hidden = false,
                        CreationDate = DateTime.UtcNow,
                        OwnerIp = CurrentUser.IPAddress,
                        OwnerId = CurrentUser.IsAnonymous ? (int?)null : CurrentUser.Id,
                        ForkedQuerySetId = context.QuerySet.Id
                    });

                    Current.DB.Execute(@"insert QuerySetRevisions(QuerySetId, RevisionId) 
select @newId, RevisionId from QuerySetRevisions where QuerySetId = @oldId", new 
                    { 
                        newId = querySetId, 
                        oldId = context.QuerySet.Id
                    });
                }
                else
                { 
                    // update it 
                    querySetId = context.QuerySet.Id;

                    context.Title = context.Title ?? context.QuerySet.Title;
                    context.Description = context.Description ?? context.QuerySet.Description;

                    Current.DB.QuerySets.Update(context.QuerySet.Id, new { context.Title, context.Description, CurrentRevisionId = revisionId, LastActivity = DateTime.UtcNow});
                    
                }
                
                Current.DB.QuerySetRevisions.Insert(new { QuerySetId = querySetId, RevisionId = revisionId });

                results.RevisionId = revisionId;
                results.Created = saveTime;
                results.QuerySetId = querySetId;
            }
            else
            {
                results.RevisionId = context.Revision != null ? context.Revision.Id : context.QuerySet.CurrentRevisionId;
                results.QuerySetId = context.QuerySet.Id;
                results.Created = null;
            }

            if (context.Title != null)
            {
                results.Slug = context.Title.URLFriendly();
            }

            QueryRunner.LogRevisionExecution(CurrentUser, siteId, results.RevisionId);

            // Consider handling this XSS condition (?) in the ToJson() method instead, if possible
            return Content(results.ToJson().Replace("/", "\\/"), "application/json");
        }


        [HttpPost]
        [StackRoute(@"query/run/{siteId:\d+}/{querySetId:\d+}/{revisionId:\d+}")]
        public ActionResult Execute(int querySetId, int revisionId, int siteId, bool? textResults, bool? withExecutionPlan, bool? bypassCache, TargetSites? targetSites)
        {
            if (CurrentUser.IsAnonymous && !CaptchaController.CaptchaPassed(GetRemoteIP()))
            {
                return Json(new { captcha = true });
            }

            ActionResult response = null;
            try
            {
                if (!ValidateTargetSites(targetSites))
                {
                    throw new ApplicationException("Invalid target sites selection");
                }

                QuerySet querySet = null;

                
                querySet = Current.DB.QuerySets.Get(querySetId);

                if (querySet == null)
                {
                    throw new ApplicationException("Invalid query set ID");
                }

                Revision revision = Current.DB.Revisions.Get(revisionId);
                if (revision == null)
                { 
                    throw new ApplicationException("Invalid revision ID");
                }

                Query query = Current.DB.Queries.Get(revision.QueryId);

                var parsedQuery = new ParsedQuery(
                    query.QueryBody,
                    Request.Params,
                    withExecutionPlan == true,
                    targetSites ?? TargetSites.Current
                );

                if (AppSettings.EnableBypassCache && bypassCache.HasValue && bypassCache.Value)
                {
                    QueryUtil.ClearCachedResults(parsedQuery, siteId);
                }

                QueryResults results = null;
                Site site = GetSite(siteId);
                ValidateQuery(parsedQuery, site);

                var contextData = new QueryContextData
                {
                    IsText = textResults == true,
                    QuerySet = querySet,
                    Revision = revision
                };

                var asyncResults = AsyncQueryRunner.Execute(parsedQuery, CurrentUser, site, contextData);

                if (asyncResults.State == AsyncQueryRunner.AsyncState.Failure)
                {
                    throw asyncResults.Exception;
                }

                if (asyncResults.State == AsyncQueryRunner.AsyncState.Success)
                {
                    results = asyncResults.QueryResults;
                }
                else
                {
                    return Json(new { running = true, job_id = asyncResults.JobId });
                }

                response = CompleteResponse(results, parsedQuery, contextData, siteId);
            }
            catch (Exception ex)
            {
                response = TransformExecutionException(ex);
            }

            return response;
        }


        [StackRoute(@"{sitename}/csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowSingleSiteCsv(string sitename, int revisionId, string slug)
        {
            Query query = QueryUtil.GetQueryForRevision(revisionId);

            if (query == null)
            {
                return PageNotFound();
            }

            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ?  (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/csv/{1}{2}{3}",
                    site.TinyName.ToLower(), revisionId, slug.HasValue() ? "/" + slug : "", Request.Url.Query 
                ));
            }

            var parsedQuery = new ParsedQuery(query.QueryBody, Request.Params);

            if (!parsedQuery.IsExecutionReady)
            {
                return PageBadRequest();
            }

            CachedResult cachedResults = QueryUtil.GetCachedResults(
                parsedQuery,
                Site.Id
            );
            List<ResultSet> resultSets;

            if (cachedResults != null)
            {
                resultSets = JsonConvert.DeserializeObject<List<ResultSet>>(cachedResults.Results, QueryResults.GetSettings());
            }
            else
            {
                resultSets = QueryRunner.GetResults(
                    parsedQuery,
                    site,
                    CurrentUser
                ).ResultSets;
            }

            return new CsvResult(resultSets);
        }


        [StackRoute(@"{sitename}/all-meta-csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowMultiSiteMeteCsv(string sitename, int revisionId)
        {
            return GetCsv(sitename, revisionId, TargetSites.AllMetaSites);
        }


        [StackRoute(@"{sitename}/all-csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowMultiSiteCsv(string sitename, int revisionId)
        {
            return GetCsv(sitename, revisionId, TargetSites.AllSites);
        }

        [StackRoute(@"{sitename}/all-non-meta-csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowMultiSiteWithoutMetaCsv(string sitename, int revisionId)
        {
            return GetCsv(sitename, revisionId, TargetSites.AllNonMetaSites);
        }

        [StackRoute(@"{sitename}/all-non-meta-but-so-csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowMultiSiteWithoutMetaExcludingSOCsv(string sitename, int revisionId)
        {
            return GetCsv(sitename, revisionId, TargetSites.AllNonMetaSitesButSO);
        }

        [StackRoute(@"{sitename}/all-meta-but-mse-csv/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowMultiSiteMeteExclusingMSOCsv(string sitename, int revisionId)
        {
            return GetCsv(sitename, revisionId, TargetSites.AllMetaSitesButMSE);
        }

        private ActionResult GetCsv(string sitename, int revisionId, TargetSites targetSites)
        {
            Query query = QueryUtil.GetQueryForRevision(revisionId);

            if (query == null)
            {
                return PageNotFound();
            }

            var results = QueryRunner.GetResults(
                new ParsedQuery(query.QueryBody, Request.Params, executionPlan: false, targetSites: targetSites),
                null,
                CurrentUser
            );

            return new CsvResult(results.ResultSets);
        }

        [StackRoute(@"{sitename}/q/{queryId:\d+}/{slug?}")]
        public ActionResult MapQuery(string sitename, int queryId, string slug)
        {
            Revision revision = QueryUtil.GetMigratedRevision(queryId, MigrationType.Normal);

            if (revision == null)
            {
                return PageNotFound();
            }

            if (slug.HasValue())
            {
                slug = "/" + slug;
            }

            // find the first queryset with the revision 
            var querySetId = Current.DB.Query<int>(@"select top 1 QuerySetId from QuerySetRevisions where RevisionId = @Id order by Id asc", new {revision.Id}).First();

            return new RedirectPermanentResult("/" + sitename + "/query/" + querySetId + slug);
        }

        [StackRoute(@"{sitename}/query/{operation:fork|edit}/{querySetId:\d+}/{slug?}")]
        public ActionResult Edit(string sitename, string operation, int querySetId, string slug)
        {
            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/query/{1}/{2}{3}",
                    site.TinyName.ToLower(), operation, querySetId, slug.HasValue() ? "/" + slug : ""
                ));
            }

            SetCommonQueryViewData(site, "Editing Query");

            QuerySet querySet = QueryUtil.GetFullQuerySet(querySetId);

            if (querySet == null)
            {
                return PageNotFound();
            }

            ViewData["query_action"] = "save/" + Site.Id +  "/" + querySetId;
            ViewData["HelperTables"] = HelperTableCache.GetCacheAsJson(Site);

            return View("Editor", new ViewModel.QuerySetViewModel 
            { 
                Site = Site, 
                Revisions = querySet.Revisions,
                CurrentRevision = querySet.CurrentRevision,
                QuerySet = querySet
            });
        }

        /// <summary>
        /// Download a query execution plan as xml.
        /// </summary>
        [StackRoute(@"{sitename}/plan/{revisionId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowPlan(string sitename, int revisionId, string slug)
        {
            Query query = QueryUtil.GetQueryForRevision(revisionId);

            if (query == null)
            {
                return PageNotFound();
            }

            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/plan/{1}{2}{3}",
                    site.TinyName.ToLower(), revisionId, slug.HasValue() ? "/" + slug : "", Request.Url.Query
                ));
            }

            var parsedQuery = new ParsedQuery(query.QueryBody, Request.Params);

            if (!parsedQuery.IsExecutionReady)
            {
                return PageBadRequest();
            }

            CachedResult cache = QueryUtil.GetCachedResults(
                parsedQuery,
                site.Id
            );

            if (cache == null || cache.ExecutionPlan == null)
            {
                return PageNotFound();
            }

            return new QueryPlanResult(cache.ExecutionPlan);
        }

        [StackRoute("{sitename}/query/new", RoutePriority.Low)]
        public ActionResult New(string sitename)
        {
            Site site;

            if (!TryGetSite(sitename, out site))
            {
                return site == null ? (ActionResult)PageNotFound() : RedirectPermanent(string.Format("/{0}/query/new",
                    site.TinyName.ToLower()
                ));
            }

            SetCommonQueryViewData(site, "Viewing Query");

            ViewData["query_action"] = "save/" + Site.Id;
            ViewData["HelperTables"] = HelperTableCache.GetCacheAsJson(Site);

            return View("Editor", null);
        }

        private QueryResults ExecuteWithResults(ParsedQuery query, int siteId, bool textResults)
        {
            QueryResults results = null;
            Site site = GetSite(siteId);
            ValidateQuery(query, site);

            results = QueryRunner.GetResults(query, site, CurrentUser);
            results = TranslateResults(query, textResults, results);
            return results;
        }

        private static QueryResults TranslateResults(ParsedQuery query, bool textResults, QueryResults results)
        {
            if (textResults)
            {
                results = results.ToTextResults();
            }

            if (query.IncludeExecutionPlan)
            {
                results = results.TransformQueryPlan();
            }
            return results;
        }

        private static void ValidateQuery(ParsedQuery query, Site site)
        {
            if (!query.IsExecutionReady)
            {
                throw new ApplicationException(!string.IsNullOrEmpty(query.ErrorMessage) ?
                    query.ErrorMessage : "All parameters must be set!");
            }

            if (site == null)
            {
                throw new ApplicationException("Invalid site ID");
            }
        }

        private static bool ValidateTargetSites(TargetSites? targetSites)
        {
            // We could check if running against only non-meta was allowed too since there's an AppSetting for it,
            // but I don't think that's necessary
            return AppSettings.AllowRunOnAllDbsOption || targetSites == null;
        }

        private ActionResult TransformExecutionException(Exception ex)
        {
            var response = new Dictionary<string, string>();
            var sqlex = ex as SqlException;

            if (sqlex != null)
            {
                response["line"] = sqlex.LineNumber.ToString();
            }

            response["error"] = ex.Message;

            return Json(response);
        }

        private void SaveMetadata(Revision revision, string title, string description, bool updateWithoutChange)
        {
            QuerySet querySet = null;

            if (title.IsNullOrEmpty())
            {
                title = null;
            }

            if (description.IsNullOrEmpty())
            {
                description = null;
            }

            if (!CurrentUser.IsAnonymous)
            {
                querySet = Current.DB.Query<QuerySet>(@"
                    select * from QuerySets
                        *
                    FROM
                        QuerySets
                    WHERE
                        InitialRevisionId = @revision AND
                        OwnerId = @owner",
                    new
                    {
                        owner = CurrentUser.Id
                    }
                ).FirstOrDefault();
            }

            // We always save a querys set for anonymous users since they don't have an
            // actual revision history that we're associating the query set with
            if (CurrentUser.IsAnonymous || querySet == null)
            {
                Current.DB.QuerySets.Insert(
                    new
                    {
                        InitialRevisionId = revision.Id,
                        CurrentRevisionId = revision.Id,
                        OwnerId = CurrentUser.IsAnonymous ? (int?)null : CurrentUser.Id,
                        Title = title,
                        Description = description,
                        LastActivity = DateTime.UtcNow,
                        Votes = 0, 
                        Views = 0
                    }
                );
            }
            else if (querySet.Title != title || querySet.Description != description)
            {
                Current.DB.QuerySets.Update(querySet.Id,
                    new
                    {
                        Title = title,
                        Description = description,
                        CurrentRevisionId = revision.Id,
                        LastActivity = DateTime.UtcNow
                    }
                );
            }
        }

        private void SetCommonQueryViewData(Site site, string header)
        {
            Site = site;
            
            SetHeader(header);
            SelectMenuItem("Compose Query");
            
            ViewData["GuessedUserId"] = Site.GuessUserId(CurrentUser);
            ViewData["Tables"] = Site.GetTableInfos();
        }
    }
}