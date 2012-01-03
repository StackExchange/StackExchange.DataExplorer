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
        /// <summary>
        /// Retrieves the basic revision information
        /// </summary>
        /// <param name="revisionId">The ID of the revision</param>
        /// <returns>A revision, or null if the ID was invalid</returns>
        public static Revision GetBasicRevision(int revisionId)
        {
            return Current.DB.Query<Revision>(
                "SELECT * FROM Revisions WHERE Id = @revision",
                new
                {
                    revision = revisionId
                }
            ).FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the cached results for the given query
        /// </summary>
        /// <param name="query">The query to retrieve cached results for</param>
        /// <param name="siteId">The site ID that the query is run against</param>
        /// <returns>The cached results, or null if no results exist in the cache</returns>
        public static CachedResult GetCachedResults(ParsedQuery query, int siteId)
        {
            if (query == null || !query.IsExecutionReady)
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

            if (cache != null && AppSettings.AutoExpireCacheMinutes >= 0 && cache.CreationDate != null)
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
        /// Retrieves a Revision with its corresponding Query and Metadata information
        /// </summary>
        /// <param name="revisionId">The ID of the revision</param>
        /// <returns>The revision, or null if the ID was invalid</returns>
        public static Revision GetCompleteRevision(int revisionId)
        {
            return Current.DB.Query<Revision, Query, QuerySet, User, Revision>(@"
                SELECT
                    *
                FROM
                    Revisions r
                JOIN
                    Queries q
                ON
                    q.Id = r.QueryId AND r.Id = @revision
                JOIN
                    QuerySets qs
                ON
                    isnull(r.RootId, r.Id) = qs.InitialRevisionId
                LEFT OUTER JOIN
                    Users u
                ON
                    r.OwnerId = u.Id",
                (revision, query, querySet, user) =>
                {
                    revision.Query = query;
                    revision.QuerySet = querySet;
                    revision.Owner = user;

                    return revision;
                },
                new
                {
                    revision = revisionId
                }
            ).FirstOrDefault();
        }

        public static Revision GetMigratedRevision(int id, MigrationType type)
        {
            return Current.DB.Query<Revision>(@"
                SELECT
                    Revisions.*
                FROM
                    Revisions
                JOIN
                    QueryMap
                ON
                    Revisions.Id = QueryMap.RevisionId
                WHERE
                    QueryMap.OriginalId = @original AND
                    MigrationType = @type",
                new
                {
                    original = id,
                    type = (int)type
                }
            ).FirstOrDefault();
        }

        public static IEnumerable<Revision> GetRevisionHistory(int rootId, int userId)
        {
            return Current.DB.Query<Revision, Query, Revision, User, Revision>(@"
                SELECT
                    revision.*, query.*, parent.*, [user].*
                FROM
                    Revisions revision
                LEFT JOIN
                    Revisions parent
                ON
                    revision.ParentId = parent.Id
                LEFT JOIN
                    Users [user]
                ON
                    parent.OwnerId = [user].Id
                JOIN
                    Queries query
                ON
                    revision.QueryId = query.Id
                WHERE
                    (revision.RootId = @root OR revision.Id = @root) AND
                    revision.OwnerId = @owner
                ORDER BY
                    revision.CreationDate DESC",
                (revision, query, parent, user) =>
                {
                    if (parent != null)
                    {
                        parent.Owner = user;
                    }

                    revision.Query = query;
                    revision.Parent = parent;

                    return revision;
                },
                new
                {
                    root = rootId,
                    owner = userId
                }
            );
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