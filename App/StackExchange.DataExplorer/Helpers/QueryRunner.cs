using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Helpers
{
    public class QueryRunner
    {
        private const int QUERY_TIMEOUT = 120;
        private const int MAX_RESULTS = 50000;

        private static readonly Dictionary<Type, ResultColumnType> ColumnTypeMap = new Dictionary<Type, ResultColumnType>
                                                                                       {
                                                                                           {
                                                                                               typeof (int),
                                                                                               ResultColumnType.Number
                                                                                               },
                                                                                           {
                                                                                               typeof (long),
                                                                                               ResultColumnType.Number
                                                                                               },
                                                                                           {
                                                                                               typeof (float),
                                                                                               ResultColumnType.Number
                                                                                               },
                                                                                           {
                                                                                               typeof (double),
                                                                                               ResultColumnType.Number
                                                                                               },
                                                                                           {
                                                                                               typeof (string),
                                                                                               ResultColumnType.Text
                                                                                               },
                                                                                           {
                                                                                               typeof (DateTime),
                                                                                               ResultColumnType.Date
                                                                                               }
                                                                                       };

        private static readonly Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>> magic_columns
            = GetMagicColumns();



        static void AddBody(StringBuilder buffer, QueryResults results, Site site)
        {
            buffer.AppendLine(site.Name);
            buffer.AppendLine("-------------------------------------------------");
            buffer.AppendLine(results.Messages);
            buffer.AppendLine();
            buffer.AppendLine();
            buffer.AppendLine();
        }

        public static void MergePivot(Site site, QueryResults current, QueryResults newResults)
        {
            int pivotIndex = -1;
            foreach (var info in newResults.ResultSets.First().Columns)
            {
                pivotIndex++;
                if (info.Name == "Pivot")
                {
                    break;
                }
            }

            var map = current
                .ResultSets
                .First()
                .Rows
                .Select(columns => new
                {
                    key = string.Join("|||", columns.Where((c, i) => i != pivotIndex && i < newResults.ResultSets.First().Columns.Count)),
                    cols = columns
                })
                .ToDictionary(r => r.key, r => r.cols);


            var newRows = new List<List<object>>();

            foreach (var row in newResults.ResultSets.First().Rows)
            {

                List<object> foundRow;
                if (map.TryGetValue(string.Join("|||", row.Where((c, i) => i != pivotIndex)), out foundRow))
                {
                    foundRow.Add(row[pivotIndex]);
                }
                else
                {
                    newRows.Add(row);
                }
            }

            current.ResultSets.First().Columns.Add(new ResultColumnInfo
            {
                Name = site.Name + " Pivot",
                Type = newResults.ResultSets.First().Columns[pivotIndex].Type
            });

            var totalColumns = current.ResultSets.First().Columns.Count;

            foreach (var row in current.ResultSets.First().Rows)
            {
                if (row.Count < totalColumns)
                {
                    row.Add(null);
                }
            }

            foreach (var row in newRows)
            {
                for (int i = pivotIndex+1; i < totalColumns; i++)
                {
                    row.Insert(pivotIndex, null);
                }
                current.ResultSets.First().Rows.Add(row);
            }
        }

        public static QueryResults GetMultiSiteResults(ParsedQuery parsedQuery, User currentUser)
        {
            var sites = Current.DB.Sites.ToList();
            if (excludeMetas)
            { 
                sites = sites.Where(s => !s.Url.Contains("meta.")).ToList();
            }


            var firstSite = sites.First();
            var results = QueryRunner.GetSingleSiteResults(parsedQuery, firstSite, currentUser);
            StringBuilder buffer = new StringBuilder();

            if (results.ResultSets.First().Columns.Where(c => c.Name == "Pivot").Any())
            {
                foreach (var info in results.ResultSets.First().Columns)
                {
                    if (info.Name == "Pivot")
                    {
                        info.Name = firstSite.Name + " Pivot";
                        break;
                    }
                }

                foreach (var s in sites.Skip(1))
                {
                    try
                    {
                        var tmp = QueryRunner.GetSingleSiteResults(parsedQuery, s, currentUser);
                        results.ExecutionTime += tmp.ExecutionTime;
                        MergePivot(s, results, tmp);
                    }
                    catch (Exception e)
                    { 
                        // don't blow up here ... just skip the site.
                    }
                }
            }
            else
            {
                results = results.ToTextResults();
                AddBody(buffer, results, firstSite);
                foreach (var s in sites.Skip(1))
                {
                    try
                    {
                        var tmp = QueryRunner.GetSingleSiteResults(parsedQuery, s, currentUser).ToTextResults();
                        results.ExecutionTime += tmp.ExecutionTime;
                        AddBody(buffer, tmp, s);
                    }
                    catch (Exception e)
                    { 
                        // don't blow up ... just skip the site
                    }

                }
            }

            results.Messages = buffer.ToString();
            results.MultiSite = true;
            results.ExcludeMetas = excludeMetas;

            return results;
        }

        public static QueryResults GetCachedResults(ParsedQuery query, Site site)
        {
            CachedResult cache = Current.DB.Query<Models.CachedResult>(
                "SELECT * FROM CachedResults WHERE QueryHash = @hash AND SiteId = @siteId",
                new
                {
                    hash = query.ExecutionHash,
                    siteId = site.Id
                }
            ).FirstOrDefault();

            QueryResults results = null;

            if (cache != null && cache.Results != null)
            {
                results = QueryResults.FromJson(cache.Results);
            }

            return results;
        }

        /// <summary>
        /// Retrieves a cached execution plan.
        /// </summary>
        /// <param name="query">The parsed query to return the execution plan for.</param>
        /// <param name="site">The site to return the execution plan for.</param>
        /// <returns>Cached execution plan, or null if there is no cached plan entry.</returns>
        /// <remarks>
        /// Note that a null return value is different from a cached plan entry with a null cache.
        /// </remarks>
        public static CachedPlan GetCachedPlan(ParsedQuery query, Site site)
        {
            return Current.DB.CachedPlans
                .Where(plan => plan.QueryHash == query.ExecutionHash && plan.SiteId == site.Id)
                .FirstOrDefault();
        }

        public static void LogQueryExecution(User user, Site site, int revisionId)
        {
            QueryExecution execution;

            execution = Current.DB.Query<QueryExecution>(@"
                SELECT
                    *
                FROM
                    QueryExecutions
                WHERE
                    RevisionId = @revision AND
                    SiteId = @site AND
                    UserId " + (user.IsAnonymous ? "IS NULL" : "= @user"),
                new
                {
                    revision = revisionId,
                    site = site.Id,
                    user = user.Id
                }
            ).FirstOrDefault();

            if (execution == null)
            {
                Current.DB.Execute(@"
                    INSERT INTO QueryExecutions(
                        ExecutionCount, FirstRun, LastRun,
                        QueryId, SiteId, UserId
                    ) VALUES(
                        1, @first, @last, @revision, @site, @user
                    )",
                    new
                    {
                        first = DateTime.UtcNow,
                        last = DateTime.UtcNow,
                        revision = revisionId,
                        site = site.Id,
                        user = user.Id
                    }
                );
            }
            else
            {
                Current.DB.Execute(@"
                    UPDATE QueryExecutions SET
                        ExecutionCount = @count,
                        LastRun = @last,
                        SiteId = @site
                    WHERE Id = @id",
                    new
                    {
                        count = execution.ExecutionCount + 1,
                        last = DateTime.UtcNow,
                        site = site.Id,
                        id = execution.ID
                    }
                );
            }
        }

        public static QueryResults ExecuteNonCached(ParsedQuery parsedQuery, Site site, User user)
        {
            return ExecuteNonCached(parsedQuery, site, user, false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Security", 
            "CA2100:Review SQL queries for security vulnerabilities", 
            Justification = "What else can I do, we are allowing users to run sql.")]
        public static QueryResults ExecuteNonCached(ParsedQuery parsedQuery, Site site, User user, bool IncludeExecutionPlan)
        {
            DBContext db = Current.DB;

            var remoteIP = OData.GetRemoteIP(); 
            var key = "total-" + remoteIP;
            var currentCount = (int?)Current.GetCachedObject(key) ?? 0;
            currentCount++;
            Current.SetCachedObjectSliding(key, currentCount, 60 * 60);

            if (currentCount > 130)
            {
                // clearly a robot, auto black list 
                var b = new BlackList { CreationDate = DateTime.UtcNow, IPAddress = remoteIP };
            }

            if (currentCount > 100)
            {
                throw new Exception("You can not run any new queries for another hour, you have exceeded your limit!");
            }

            if (db.ExecuteQuery<int>("select count(*) from BlackList where IPAddress = {0}", remoteIP).First() > 0)
            {
                System.Threading.Thread.Sleep(2000);
                throw new Exception("You have been blacklisted due to abuse!");
            }

            var results = new QueryResults();

            results.Url = site.Url;
            results.SiteId = site.Id;
            results.SiteName = site.Name.ToLower();

            using (SqlConnection cnn = site.GetConnection())
            {
                cnn.Open();

                var timer = new Stopwatch();
                timer.Start();

                var messages = new StringBuilder();

                var infoHandler = new SqlInfoMessageEventHandler((sender, args) =>
                                                                     {
                                                                         // todo handle errors as well
                                                                         messages.AppendLine(args.Message);
                                                                     });

                try
                {
                    cnn.InfoMessage += infoHandler;

                    if (IncludeExecutionPlan)
                    {
                        using (var command = new SqlCommand("SET STATISTICS XML ON", cnn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }

                    var plan = new QueryPlan();
                    foreach (string batch in parsedQuery.ExecutionSqlBatches)
                    {
                        using (var command = new SqlCommand(batch, cnn))
                        {
                            command.CommandTimeout = QUERY_TIMEOUT;
                            PopulateResults(results, command, messages, IncludeExecutionPlan);
                        }
                        if (IncludeExecutionPlan)
                        {
                            plan.AppendBatchPlan(results.ExecutionPlan);
                            results.ExecutionPlan = null;
                        }
                    }
                    results.ExecutionPlan = plan.PlanXml;
                }
                finally
                {
                    cnn.InfoMessage -= infoHandler;
                    results.Messages = messages.ToString();
                }

                timer.Stop();
                results.ExecutionTime = timer.ElapsedMilliseconds;

                ProcessMagicColumns(results, cnn);

            }


            Query query = db.Queries
                .Where(q => q.QueryHash == parsedQuery.Hash)
                .FirstOrDefault();

            if (query == null)
            {
                query = new Query
                            {
                                CreatorIP = user.IPAddress,
                                FirstRun = DateTime.UtcNow,
                                QueryBody = parsedQuery.RawSql,
                                QueryHash = parsedQuery.Hash,
                                Views = 0,
                                CreatorId = user.IsAnonymous ? (int?) null : user.Id,
                                Name = parsedQuery.Name,
                                Description = parsedQuery.Description
                            };

                db.Queries.InsertOnSubmit(query);
            }
            else
            {
                if (!user.IsAnonymous)
                {
                    query.Name = parsedQuery.Name;
                    query.Description = parsedQuery.Description;
                }
            }

            db.SubmitChanges();

            results.QueryId = query.Id;

            db.SubmitChanges();

            results.Slug = query.Name.URLFriendly();

            return results;
        }

        /// <summary>
        /// Executes an SQL query and populates a given <see cref="QueryResults" /> instance with the results.
        /// </summary>
        /// <param name="results"><see cref="QueryResults" /> instance to populate with results.</param>
        /// <param name="command">SQL command to execute.</param>
        /// <param name="messages"><see cref="StringBuilder" /> instance to which to append messages.</param>
        private static void PopulateResults(QueryResults results, SqlCommand command, StringBuilder messages)
        {
            PopulateResults(results, command, messages, false);
        }

        /// <summary>
        /// Executes an SQL query and populates a given <see cref="QueryResults" /> instance with the results.
        /// </summary>
        /// <param name="results"><see cref="QueryResults" /> instance to populate with results.</param>
        /// <param name="command">SQL command to execute.</param>
        /// <param name="messages"><see cref="StringBuilder" /> instance to which to append messages.</param>
        /// <param name="IncludeExecutionPlan">If true indciates that the query execution plans are expected to be contained
        /// in the results sets; otherwise, false.</param>
        private static void PopulateResults(QueryResults results, SqlCommand command, StringBuilder messages, bool IncludeExecutionPlan)
        {
            QueryPlan plan = new QueryPlan();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                do
                {
                    // Check to see if the resultset is an execution plan
                    if (IncludeExecutionPlan && reader.FieldCount == 1 && reader.GetName(0) == "Microsoft SQL Server 2005 XML Showplan")
                    {
                        if (reader.Read())
                        {
                            plan.AppendStatementPlan(reader[0].ToString());
                        }
                    }
                    else
                    {
                        if (reader.FieldCount == 0)
                        {
                            if (reader.RecordsAffected >= 0)
                            {
                                messages.AppendFormat("({0} row(s) affected)\n\n", reader.RecordsAffected);
                            }
                            continue;
                        }

                        var resultSet = new ResultSet();
                        resultSet.MessagePosition = messages.Length;
                        results.ResultSets.Add(resultSet);

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var columnInfo = new ResultColumnInfo();
                            columnInfo.Name = reader.GetName(i);
                            ResultColumnType colType;
                            if (ColumnTypeMap.TryGetValue(reader.GetFieldType(i), out colType))
                            {
                                columnInfo.Type = colType;
                            }

                            resultSet.Columns.Add(columnInfo);
                        }

                        int currentRow = 0;
                        while (reader.Read())
                        {
                            if (currentRow++ >= MAX_RESULTS)
                            {
                                results.Truncated = true;
                                results.MaxResults = MAX_RESULTS;
                                break;
                            }
                            var row = new List<object>();
                            resultSet.Rows.Add(row);

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                object col = reader.GetValue(i);
                                if (col is DateTime)
                                {
                                    var date = (DateTime)col;
                                    col = date.ToString("yyyy-MM-dd H:mm:ss");
                                }
                                row.Add(col);
                            }
                        }
                        if (results.Truncated)
                        {
                            // next result would force ado.net to fast forward
                            //  through the result set, which is way too slow
                            break;
                        }

                        if (reader.RecordsAffected >= 0)
                        {
                            messages.AppendFormat("({0} row(s) affected)\n\n", reader.RecordsAffected);
                        }

                        messages.AppendFormat("({0} row(s) affected)\n\n", resultSet.Rows.Count);
                    }
                } while (reader.NextResult());
                command.Cancel();
            }
            results.ExecutionPlan = plan.PlanXml;
        }

        public static QueryResults GetSingleSiteResults(ParsedQuery parsedQuery, Site site, User user)
        {
            DBContext db = Current.DB;

            var cache = GetCachedResults(parsedQuery, site);

            VerifyQueryState(parsedQuery, cache, user);

            if (cache != null)
            {
                if (!IncludeExecutionPlan)
                {
                    return cache;
                }
                else
                {
                    var plan = GetCachedPlan(parsedQuery, site);

                    if (plan != null)
                    {
                        cache.ExecutionPlan = plan.Plan;

                        return cache;
                    }
                }
            }

            QueryResults results = ExecuteNonCached(parsedQuery, site, user, IncludeExecutionPlan);

            if (cache == null)
            {
                AddResultToCache(results, parsedQuery, site, db, IncludeExecutionPlan);
            }

            if (IncludeExecutionPlan)
            {
                db.CachedPlans.InsertOnSubmit(new CachedPlan()
                {
                    QueryHash = parsedQuery.ExecutionHash,
                    SiteId = site.Id,
                    Plan = results.ExecutionPlan,
                    CreationDate = DateTime.UtcNow,
                });
            }

            db.SubmitChanges();

            return results;
        }

        /// <summary>
        /// Verifies that the cached set of results are correct for this particular query,
        /// addressing the case where many queries may produce the same execution SQL, but may
        /// have differing parameter names.
        /// </summary>
        /// <param name="parsedQuery">The current query that produces the results in the
        /// cache</param>
        /// <param name="cache">The cached results for a query semantically equivalent to the
        /// provided one</param>
        /// <param name="user"></param>
        private static void VerifyQueryState(ParsedQuery parsedQuery, QueryResults cache, User user)
        {
            Query query = Current.DB.Query<Query>(
                "SELECT * FROM Queries WHERE QueryHash = @hash",
                new
                {
                    hash = parsedQuery.Hash
                }
            ).FirstOrDefault();

            // Check if the parsed query is actually responsible for this cache
            if (cache != null)
            {
                int id;

                // If the query doesn't exist, now is the only time it'll get created since it
                // corresponds to a pre-existing cache result (but isn't actually what generated
                // the result set)
                if (query == null)
                {
                    id = (int)Current.DB.Query<decimal>(@"
                        INSERT INTO Queries(
                            CreatorId, CreatorIP, FirstRun, Name,
                            Description, QueryBody, QueryHash, Views
                        ) VALUES(
                            @CreatorId, @CreatorIP, @FirstRun, @Name,
                            @Description, @QueryBody, @QueryHash, @Views
                        )

                        SELECT SCOPE_IDENTITY()",
                        new
                        {
                            CreatorId = user.IsAnonymous ? (int?)null : user.Id,
                            CreatorIP = user.IPAddress,
                            FirstRun = DateTime.UtcNow,
                            Name = parsedQuery.Name,
                            Description = parsedQuery.Description,
                            QueryBody = parsedQuery.RawSql,
                            QueryHash = parsedQuery.Hash,
                            Views = 0
                        }
                    ).First();
                }
                else
                {
                    id = query.Id;
                }

                // Update the cache's query ID to lie to the client that these results came from
                // this specific query
                if (id != cache.QueryId)
                {
                    cache.QueryId = id;
                }
            }

            // Update the important bits of the query if we didn't just insert it, provided that
            // the user isn't anonymous.
            if (query != null && !user.IsAnonymous)
            {
                // Save ourselves the trouble of updating if nothing's changed
                if (query.Name != parsedQuery.Name || query.Description != parsedQuery.Description || query.QueryBody != parsedQuery.RawSql)
                {
                    Current.DB.Execute(@"
                        UPDATE Queries SET
                            Name = @name,
                            Description = @description,
                            QueryBody = @sql
                        WHERE Id = @id",
                        new
                        {
                            name = parsedQuery.Name,
                            description = parsedQuery.Description,
                            sql = parsedQuery.RawSql,
                            id = query.Id
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Adds a query result to the cache.
        /// </summary>
        /// <param name="json">Query results to add to the cache.</param>
        /// <param name="IncludeExecutionPlan">If true indicates that the result set includes a query
        /// execution plan; otherwise, false.</param>
        private static void AddResultToCache(QueryResults results, ParsedQuery parsedQuery, Site site, DBContext db, bool IncludeExecutionPlan)
        {
            // Avoid saving the execution plan as part of the results
            string executionPlan = results.ExecutionPlan;
            results.ExecutionPlan = null;

            // After this method is called, the execution plan is cached in a separate (but very
            // similar) way. It might make more sense to just include it in this table so that we
            // don't have to make multiple queries, but I'd have to investigate it more.
            db.Execute(@"
                INSERT INTO CachedResults(
                    QueryHash, SiteId, Results, CreationDate
                ) VALUES(
                    @hash, @site, @results, @creation
                )",
                new
                {
                    hash = parsedQuery.ExecutionHash,
                    site = site.Id,
                    // I removed the anti-XSS replace here because this should always pass through
                    // the one that's now called on all results in the controller
                    results = results.ToJson(),
                    creation = DateTime.UtcNow
                }
            );

            results.ExecutionPlan = executionPlan;
        }

        private static void ProcessMagicColumns(QueryResults results, SqlConnection cnn)
        {
            int index = 0;
            foreach (ResultSet resultSet in results.ResultSets)
            {
                foreach (ResultColumnInfo column in resultSet.Columns)
                {
                    if (magic_columns.ContainsKey(column.Name))
                    {
                        DecorateColumn(column);
                        IEnumerable<object> values = resultSet.Rows.Select(row => row[index]);
                        List<object> processedValues = magic_columns[column.Name](cnn, values);
                        int rowNumber = 0;
                        foreach (var row in resultSet.Rows)
                        {
                            row[index] = processedValues[rowNumber];
                            rowNumber++;
                        }
                    }
                    index++;
                }
            }
        }

        private static void DecorateColumn(ResultColumnInfo column)
        {
            switch (column.Name)
            {
                case "Post Link":
                    column.Type = ResultColumnType.Post;
                    break;
                case "User Link":
                    column.Type = ResultColumnType.User;
                    break;
                case "Suggested Edit Link":
                    column.Type = ResultColumnType.SuggestedEdit;
                    break;
                default:
                    break;
            }
        }

        private static Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>> GetMagicColumns()
        {
            var rval = new Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>>();
            rval["Post Link"] = GetPostLinks;
            rval["User Link"] = GetUserLinks;
            rval["Suggested Edit Link"] = GetSuggestedEditLinks;
            return rval;
        }

        public static List<object> GetSuggestedEditLinks(SqlConnection cnn, IEnumerable<object> items)
        {
            return LookupIds(cnn, items,
                             @"select Id, case when RejectionDate is not null then 'rejected' when ApprovalDate is not null then 'accepted' else 'pending' end 
                            from SuggestedEdits
                            where Id in ");
        }

        public static List<object> GetUserLinks(SqlConnection cnn, IEnumerable<object> items)
        {
            return LookupIds(cnn, items,
                             @"select Id, case when DisplayName is null or LEN(DisplayName) = 0 then 'unknown' else DisplayName end from Users where Id in ");
        }

        public static List<object> GetPostLinks(SqlConnection cnn, IEnumerable<object> items)
        {
            return LookupIds(cnn, items,
                             @"select p1.Id, isnull(p1.Title,p2.Title) from Posts p1 
                  left join Posts p2 on p1.ParentId = p2.Id where p1.Id in ");
        }


        public static List<object> LookupIds(SqlConnection cnn, IEnumerable<object> items, string lookupSql)
        {
            var rval = new List<object>();
            if (items.Count() == 0) return rval;

            // safe due to the long cast (not that it matters, it runs read only) 
            string list = String.Join(" , ",
                                      items.Where(i => i != null && i is int).Select(i => ((int) i).ToString()).ToArray());
            if (list == "")
            {
                return items.ToList();
            }

            StringBuilder query = new StringBuilder()
                .Append(lookupSql)
                .Append("( ")
                .Append(list)
                .Append(" ) ");

            var linkMap = new Dictionary<long, object>();
            using (SqlCommand cmd = cnn.CreateCommand())
            {
                cmd.CommandText = query.ToString();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var extraInfo = new Dictionary<string, object>();
                        extraInfo["title"] = reader.IsDBNull(1) ? "unknown" : reader.GetString(1);
                        extraInfo["id"] = reader.GetInt32(0);
                        linkMap[reader.GetInt32(0)] = extraInfo;
                    }
                }
            }


            foreach (object item in items)
            {
                if (item == null || !(item is int))
                {
                    rval.Add(item);
                }
                else
                {
                    try
                    {
                        rval.Add(linkMap[(int) item]);
                    }
                    catch
                    {
                        // this is exceptional
                        rval.Add(item);
                    }
                }
            }

            return rval;
        }
    }
}