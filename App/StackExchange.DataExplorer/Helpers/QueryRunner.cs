using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Web;
using StackExchange.DataExplorer.Models;
using Dapper;

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
                                                                                               typeof (decimal),
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
            if (parsedQuery.ExcludesMetas)
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
            results.ExcludeMetas = parsedQuery.ExcludesMetas;

            return results;
        }

        public static void LogQueryExecution(User user, int siteId, int revisionId, int queryId)
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
                    site = siteId,
                    user = user.Id
                }
            ).FirstOrDefault();

            if (execution == null)
            {
                Current.DB.Execute(@"
                    INSERT INTO QueryExecutions(
                        ExecutionCount, FirstRun, LastRun,
                        RevisionId, QueryId, SiteId, UserId
                    ) VALUES(
                        1, @first, @last, @revision, @query, @site, @user
                    )",
                    new
                    {
                        first = DateTime.UtcNow,
                        last = DateTime.UtcNow,
                        revision = revisionId,
                        query = queryId,
                        site = siteId,
                        user = user.Id
                    }
                );
            }
            else
            {
                Current.DB.Execute(@"
                    UPDATE QueryExecutions SET
                        ExecutionCount = @count,
                        LastRun = @last
                    WHERE Id = @id",
                    new
                    {
                        count = execution.ExecutionCount + 1,
                        last = DateTime.UtcNow,
                        id = execution.Id
                    }
                );
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Security", 
            "CA2100:Review SQL queries for security vulnerabilities", 
            Justification = "What else can I do, we are allowing users to run sql.")]
        public static QueryResults ExecuteNonCached(ParsedQuery query, Site site, User user)
        {
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

            if (Current.DB.ExecuteQuery<int>("select count(*) from BlackList where IPAddress = {0}", remoteIP).First() > 0)
            {
                System.Threading.Thread.Sleep(2000);
                throw new Exception("You have been blacklisted due to abuse!");
            }

            var results = new QueryResults();

            using (SqlConnection cnn = site.GetConnection())
            {
                cnn.Open();

                // well we do not want to risk blocking, if somebody needs to change this we will need to add a setting
                cnn.Execute("set transaction isolation level read uncommitted");

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

                    if (query.IncludeExecutionPlan)
                    {
                        using (var command = new SqlCommand("SET STATISTICS XML ON", cnn))
                        {
                            command.ExecuteNonQuery();
                        }
                    }

                    var plan = new QueryPlan();

                    foreach (string batch in query.ExecutionSqlBatches)
                    {
                        using (var command = new SqlCommand(batch, cnn))
                        {
                            command.CommandTimeout = QUERY_TIMEOUT;
                            PopulateResults(results, command, messages, query.IncludeExecutionPlan);
                        }

                        if (query.IncludeExecutionPlan)
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

            return results;
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

        public static QueryResults GetSingleSiteResults(ParsedQuery query, Site site, User user)
        {
            QueryResults results = null;
            var timer = new Stopwatch();

            timer.Start();

            var cache = QueryUtil.GetCachedResults(query, site.Id);

            if (cache != null)
            {
                if (!query.IncludeExecutionPlan || cache.ExecutionPlan != null)
                {
                    results = new QueryResults();
                    results.WithCache(cache);
                    results.Truncated = cache.Truncated;
                    results.Messages = cache.Messages;
                    results.FromCache = true;

                    // If we didn't ask for the execution plan, don't return it
                    if (!query.IncludeExecutionPlan)
                    {
                        results.ExecutionPlan = null;
                    }
                }
            }

            timer.Stop();

            if (results == null)
            {
                results = ExecuteNonCached(query, site, user);
                results.FromCache = false;

                AddResultToCache(results, query, site, cache != null);
            }
            else
            {
                results.ExecutionTime = timer.ElapsedMilliseconds;
            }

            results.Url = site.Url;
            results.SiteId = site.Id;
            results.SiteName = site.Name.ToLower();

            return results;
        }

        /// <summary>
        /// Adds the results of a running a particular query for a given site to the database cache
        /// </summary>
        /// <param name="results">The results of the query</param>
        /// <param name="query">The query that was executed</param>
        /// <param name="site">The site that the query was run against</param>
        /// <param name="planOnly">Whether or not this is just an update to add the cached execution plan</param>
        private static void AddResultToCache(QueryResults results, ParsedQuery query, Site site, bool planOnly)
        {
            if (!planOnly)
            {
                Current.DB.Execute(@"
                    INSERT INTO CachedResults(
                        QueryHash, SiteId, Results, ExecutionPlan,
                        Messages, Truncated, CreationDate
                    ) VALUES(
                        @hash, @site, @results, @plan,
                        @messages, @truncated, @creation
                    )",
                    new
                    {
                        hash = query.ExecutionHash,
                        site = site.Id,
                        results = results.GetJsonResults(),
                        plan = results.ExecutionPlan,
                        messages = results.Messages,
                        truncated = results.Truncated,
                        creation = DateTime.UtcNow
                    }
                );
            }
            else
            {
                // Should we just update everything in this case? Presumably the only
                // thing that changed was the addition of the execution plan, but...
                Current.DB.Execute(@"
                    UPDATE
                        CachedResults
                    SET
                        ExecutionPlan = @plan
                    WHERE
                        QueryHash = @hash",
                    new
                    {
                        plan = results.ExecutionPlan,
                        hash = query.ExecutionHash
                    }
                );
            }
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