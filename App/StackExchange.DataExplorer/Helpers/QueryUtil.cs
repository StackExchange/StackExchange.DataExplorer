using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Helpers
{
    public class QueryUtil
    {

        public static QuerySet GetFullQuerySet(int querySetId)
        {
            var querySet = Current.DB.QuerySets.Get(querySetId);
            if (querySet == null) return null;

            querySet.Revisions = Current.DB.Query<Revision>(@"select r.* from QuerySetRevisions qr 
join Revisions r on r.Id = qr.RevisionId 
where qr.QuerySetId = @querySetId
order by qr.Id asc", new {querySetId}).ToList();

            var queries = Current.DB.Query<Query>(@"select * from Queries where Id in @Ids", new { Ids = querySet.Revisions.Select(r => r.QueryId).Distinct() }).ToDictionary(q => q.Id);
            var usersToLoad = querySet.Revisions.Select(r => r.OwnerId).Concat(new[] {querySet.OwnerId}).Where(id => id != null).ToArray();

            // shallow load users, pulling about me seems overkill
            var users = Current.DB.Query<User>("select Id, Login, Email, IPAddress from Users where Id in @Ids", new { Ids = usersToLoad }).ToDictionary(u => u.Id);

            Func<int?, string, User> getUser = (id, ip) => 
            {
                User user = null;
                if (id.HasValue)
                {
                    users.TryGetValue(id.Value, out user);
                }
                user = user ?? new User { IPAddress = ip, IsAnonymous = true };
                return user;
            };

            querySet.Owner = getUser(querySet.OwnerId, querySet.OwnerIp);
            querySet.CurrentRevision = querySet.Revisions.Last();
            querySet.InitialRevision = querySet.Revisions.First();

            foreach (var revision in querySet.Revisions)
            {
                revision.QuerySet = querySet;
                revision.Owner = getUser(revision.OwnerId, revision.OwnerIP);
                Query query = null;
                queries.TryGetValue(revision.QueryId, out query);
                revision.Query = query;
            }

            return querySet;
        }


        /// <summary>
        /// Retrieves the cached results for the given query
        /// </summary>
        /// <param name="query">The query to retrieve cached results for</param>
        /// <param name="siteId">The site ID that the query is run against</param>
        /// <returns>The cached results, or null if no results exist in the cache</returns>
        public static CachedResult GetCachedResults(ParsedQuery query, int siteId)
        {
            if (query == null || !query.IsExecutionReady || AppSettings.AutoExpireCacheMinutes == 0)
            {
                return null;
            }

            var cache = Current.DB.Query<CachedResult>(@"
                SELECT
                    *
                FROM
                    CachedResults
                WHERE
                    QueryHash = @hash AND
                    SiteId = @site",
                new
                {
                    hash = query.ExecutionHash,
                    site = siteId
                }
            ).FirstOrDefault();

            if (cache != null && AppSettings.AutoExpireCacheMinutes > 0 && cache.CreationDate != null)
            {
                if (cache.CreationDate.Value.AddMinutes(AppSettings.AutoExpireCacheMinutes) < DateTime.UtcNow)
                {
                    Current.DB.Execute("DELETE CachedResults WHERE Id = @id", new { id = cache.Id });

                    cache = null;
                }
            }

            return cache;
        }

        /// <summary>
        /// Clears the cached results for the given query
        /// </summary>
        /// <param name="query">The query to clear cache for</param>
        /// <param name="siteId">The site ID that the query is run against</param>
        public static void ClearCachedResults(ParsedQuery query, int siteId)
        {
            if (query == null || !query.IsExecutionReady || AppSettings.AutoExpireCacheMinutes == 0)
            {
                return;
            }

            Current.DB.Query<CachedResult>(@"
                DELETE
                    CachedResults
                WHERE
                    QueryHash = @hash AND
                    SiteId = @site",
                new
                {
                    hash = query.ExecutionHash,
                    site = siteId
                }
            );
        }

        public static Revision GetMigratedRevision(int id, MigrationType type)
        {
            return Current.DB.Query<Revision,QuerySet,Revision>(@"
                SELECT
                    r.*, qs.*
                FROM
                    Revisions r
                JOIN
                    QueryMap qm
                ON
                    r.Id = qm.RevisionId
                JOIN 
                    QuerySets qs on qs.Id = r.OriginalQuerySetId 
                WHERE
                    qm.OriginalId = @original AND
                    MigrationType = @type",(r,q) => 
                {
                    r.QuerySet = q;
                    return r;
                },
                new
                {
                    original = id,
                    type = (int)type
                }
            ).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the Query linked to the provided revision
        /// </summary>
        /// <param name="revisionId">The ID of the revision</param>
        /// <returns>Linked query, or null if the ID was invalid</returns>
        public static Query GetQueryForRevision(int revisionId)
        {
            return Current.DB.Query<Query>(@"
                SELECT
                    *
                FROM
                    Queries JOIN
                    Revisions ON Queries.Id = Revisions.QueryId AND Revisions.Id = @revision
                ",
                new
                {
                    revision = revisionId
                }
            ).FirstOrDefault();
        }
    }
}