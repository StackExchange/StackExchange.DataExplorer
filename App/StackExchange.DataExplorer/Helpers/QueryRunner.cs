using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackExchange.DataExplorer.Models;
using Dapper;

namespace StackExchange.DataExplorer.Helpers
{
    public class QueryRunner
    {
        private static readonly Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>> _magicColumns
            = GetMagicColumns();

        public static readonly Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>>.KeyCollection MagicColumnNames = _magicColumns.Keys;
        
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
                Name = site.LongName + " Pivot",
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

        private static QueryResults GetMultiSiteResults(ParsedQuery parsedQuery, User currentUser, AsyncQueryRunner.AsyncResult result = null)
        {
            var sites = Current.DB.Sites.All();
            if (parsedQuery.TargetSites == TargetSites.AllNonMetaSites)
            { 
                sites = sites.Where(s => !s.Url.Contains("meta.")).ToList();
            }
            else if (parsedQuery.TargetSites == TargetSites.AllMetaSites)
            {
                sites = sites.Where(s => s.Url.Contains("meta.")).ToList();
            }
            else if (parsedQuery.TargetSites == TargetSites.AllNonMetaSitesButSO)
            {
                sites = sites.Where(s => !s.Url.Contains("meta.") && !s.Url.Contains("stackoverflow.")).ToList();
            }
            else if (parsedQuery.TargetSites == TargetSites.AllMetaSitesButMSE)
            {
                sites = sites.Where(s => s.Url.Contains("meta.") && s.Url != "http://meta.stackexchange.com").ToList();
            }

            var firstSite = sites.First();
            var results = GetSingleSiteResults(parsedQuery, firstSite, currentUser, result);

            if (results.ResultSets.First().Columns.Any(c => c.Name == "Pivot"))
            {
                foreach (var info in results.ResultSets.First().Columns)
                {
                    if (info.Name == "Pivot")
                    {
                        info.Name = firstSite.LongName + " Pivot";
                        break;
                    }
                }

                foreach (var s in sites.Skip(1))
                {
                    try
                    {
                        var tmp = GetSingleSiteResults(parsedQuery, s, currentUser);
                        results.ExecutionTime += tmp.ExecutionTime;
                        MergePivot(s, results, tmp);
                    }
                    catch (Exception)
                    { 
                        // don't blow up here ... just skip the site.
                    }
                }
            }
            else
            {

                results.ResultSets[0].Columns.Add(new ResultColumnInfo { Name = "Site Name", Type = ResultColumnType.Site });
                foreach (var row in results.ResultSets[0].Rows)
                {
                    row.Add(sites.First().SiteInfo);
                }
                
                foreach (var s in sites.Skip(1))
                {
                    if (result != null && result.Cancelled)
                    {
                        break;
                    }

                    try
                    {
                        var tmp = GetSingleSiteResults(parsedQuery, s, currentUser, result);

                        foreach (var row in tmp.ResultSets[0].Rows)
                        {
                            row.Add(s.SiteInfo);
                            results.ResultSets[0].Rows.Add(row);
                        }

                        results.ExecutionTime += tmp.ExecutionTime;
                        results.Messages += "\n" + tmp.Messages;
                    }
                    catch (Exception)
                    { 
                        // don't blow up ... just skip the site
                    }

                }
            }

            results.TargetSites = parsedQuery.TargetSites;

            return results;
        }

        public static void LogRevisionExecution(User user, int siteId, int revisionId)
        {

            int updated = Current.DB.Query<int>(@"
                UPDATE RevisionExecutions 
                   SET ExecutionCount = ExecutionCount + 1,
                       LastRun = @last
                 WHERE RevisionId = @revision
                   AND SiteId = @site
                   AND UserId " + (user.IsAnonymous ? "IS NULL" : "= @user") + @"

                SELECT @@ROWCOUNT",
                new
                {
                    revision = revisionId,
                    site = siteId,
                    user = user.Id,
                    last = DateTime.UtcNow
                }
            ).FirstOrDefault();

            if (updated == 0)
            {
                Current.DB.Execute(@"
                    INSERT INTO RevisionExecutions(
                        ExecutionCount, FirstRun, LastRun,
                        RevisionId, SiteId, UserId
                    ) VALUES(
                        1, @first, @last, @revision, @site, @user
                    )",
                    new
                    {
                        first = DateTime.UtcNow,
                        last = DateTime.UtcNow,
                        revision = revisionId,
                        site = siteId,
                        user = user.IsAnonymous ? (int?)null : user.Id
                    }
                );
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Security", 
            "CA2100:Review SQL queries for security vulnerabilities", 
            Justification = "What else can I do, we are allowing users to run sql.")]
        public static QueryResults ExecuteNonCached(ParsedQuery query, Site site, User user, AsyncQueryRunner.AsyncResult result)
        {
            var remoteIP = OData.GetRemoteIP(); 
            var key = "total-" + remoteIP;
            var currentCount = (int?)Current.GetCachedObject(key) ?? 0;
            currentCount++;
            Current.SetCachedObjectSliding(key, currentCount, 60 * 60);

            if (currentCount > 130)
            {
                // clearly a robot, auto black list 
                Current.DB.BlackList.Insert(new { CreationDate = DateTime.UtcNow, IPAddress = remoteIP });
            }

            if (currentCount > 100)
            {
                throw new Exception("You can not run any new queries for another hour, you have exceeded your limit!");
            }

            if (Current.DB.Query<int>("select count(*) from BlackList where IPAddress = @remoteIP", new { remoteIP }).First() > 0)
            {
                System.Threading.Thread.Sleep(2000);
                throw new Exception("You have been blacklisted due to abuse!");
            }

            var results = new QueryResults();

            using (SqlConnection cnn = site.GetOpenConnection())
            {
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

                    foreach (var batch in query.ExecutionSqlBatches)
                    {
                        using (var command = new SqlCommand(batch, cnn))
                        {
                            if (result != null)
                            {
                                result.Command = command;
                                if (result.Cancelled)
                                {
                                    continue;
                                }
                            }
                            command.CommandTimeout = AppSettings.QueryTimeout;

                            try
                            {
                                PopulateResults(results, command, result, messages, query.IncludeExecutionPlan);
                            }
                            catch (Exception ex)
                            {
                                // Ugh. So, if we cancel the query in-process, we get an exception...
                                // But we have no good way of knowing that the exception here is actually
                                // *that* exception...so we'll just assume it was if the state is Cancelled
                                if (result == null || result.State != AsyncQueryRunner.AsyncState.Cancelled)
                                {
                                    throw ex;
                                }
                            }
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
        /// <param name="result"><see cref="AsyncResult"/> instance to use to mark state changes.</param>
        /// <param name="messages"><see cref="StringBuilder" /> instance to which to append messages.</param>
        /// <param name="IncludeExecutionPlan">If true indciates that the query execution plans are expected to be contained
        /// in the results sets; otherwise, false.</param>
        private static void PopulateResults(QueryResults results, SqlCommand command, AsyncQueryRunner.AsyncResult result, StringBuilder messages, bool IncludeExecutionPlan)
        {
            var plan = new QueryPlan();
            using (var reader = command.ExecuteReader())
            {
                if (result != null && reader.HasRows)
                {
                    result.HasOutput = true;
                }

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
                    else if (reader.FieldCount != 0)
                    {
                        var resultSet = new ResultSet {MessagePosition = messages.Length};
                        results.ResultSets.Add(resultSet);

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var columnInfo = new ResultColumnInfo {Name = reader.GetName(i)};
                            ResultColumnType colType;
                            if (ResultColumnInfo.ColumnTypeMap.TryGetValue(reader.GetFieldType(i), out colType))
                            {
                                columnInfo.Type = colType;
                            }

                            resultSet.Columns.Add(columnInfo);
                        }

                        int currentRow = 0, totalRows = results.TotalResults;

                        while (reader.Read())
                        {
                            if (++currentRow > AppSettings.MaxResultsPerResultSet || totalRows + currentRow > AppSettings.MaxTotalResults)
                            {
                                resultSet.Truncated = true;

                                break;
                            }

                            var row = new List<object>();

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                object col = reader.GetValue(i);

                                if (col is DBNull)
                                {
                                    col = null;
                                }
                                else if (col is DateTime)
                                {
                                    col = ((DateTime)col).ToJavascriptTime();
                                }

                                row.Add(col);
                            }

                            resultSet.Rows.Add(row);
                        }

                        results.TotalResults = totalRows + resultSet.Rows.Count;

                        if (totalRows + currentRow > AppSettings.MaxTotalResults)
                        {
                            results.Truncated = true;

                            // next result would force ado.net to fast forward through the result set, which is way too slow
                            break;
                        }

                        messages.AppendFormat("({0} row(s) returned)\n\n", resultSet.Rows.Count);
                    }

                    if (reader.RecordsAffected >= 0)
                    {
                        messages.AppendFormat("({0} row(s) affected)\n\n", reader.RecordsAffected);
                    }
                } while (reader.NextResult());

                command.Cancel();
            }

            results.ExecutionPlan = plan.PlanXml;
        }

        public static QueryResults GetResults(ParsedQuery query, Site site, User user, AsyncQueryRunner.AsyncResult result = null)
        {
            return query.TargetSites != TargetSites.Current
                ? GetMultiSiteResults(query, user, result)
                : GetSingleSiteResults(query, site, user, result);
        }

        private static QueryResults GetSingleSiteResults(ParsedQuery query, Site site, User user, AsyncQueryRunner.AsyncResult result = null)
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
                results = ExecuteNonCached(query, site, user, result);
                results.FromCache = false;

                // Don't cache cancelled results, since we don't know what state they're in...
                if (result != null && !result.Cancelled)
                {
                    AddResultToCache(results, query, site, cache != null);
                }
            }
            else
            {
                results.ExecutionTime = timer.ElapsedMilliseconds;
            }

            results.Url = site.Url;
            results.SiteId = site.Id;
            results.SiteName = site.TinyName.ToLower();

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
            // If the cache time is zero, just don't save a cache
            if (AppSettings.AutoExpireCacheMinutes == 0)
            {
                return;
            }

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
                    UPDATE CachedResults
                       SET ExecutionPlan = @plan
                     WHERE QueryHash = @hash",
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
            foreach (var resultSet in results.ResultSets)
            {
                int index = 0;
                foreach (var column in resultSet.Columns)
                {
                    if (_magicColumns.ContainsKey(column.Name))
                    {
                        DecorateColumn(column);

                        // tricky ... multi site has special handling.
                        if (resultSet.Columns.Any(c => c.Type == ResultColumnType.Site))
                        {
                            int siteNameIndex = 0;
                            foreach (var item in resultSet.Columns)
                            {
                                if (item.Type == ResultColumnType.Site) break;
                                siteNameIndex++;
                            }

                            var sites = Current.DB.Sites.All();
                            foreach (var group in resultSet.Rows.GroupBy(r => r[siteNameIndex]))
                            {
                                using (var newConnection = sites.First(s => s.Id == ((SiteInfo)group.First()[siteNameIndex]).Id).GetOpenConnection())
                                {
                                    ProcessColumn(newConnection, index, group.ToList(), column);
                                }
                            }
                        }
                        else
                        {
                            ProcessColumn(cnn, index, resultSet.Rows, column);
                        }
                    }

                    index++;
                }
            }
        }

        private static void ProcessColumn(SqlConnection cnn, int index, List<List<object>> rows, ResultColumnInfo column)
        {
            var values = rows.Select(row => row[index]);
            var processedValues = _magicColumns[column.Name](cnn, values);
            int rowNumber = 0;
            foreach (var row in rows)
            {
                row[index] = processedValues[rowNumber];
                rowNumber++;
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
                case "Comment Link":
                    column.Type = ResultColumnType.Comment;
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
            return new Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>>
            {
                { "Post Link", GetPostLinks },
                { "User Link", GetUserLinks },
                { "Comment Link", GetCommentLinks },
                { "Suggested Edit Link", GetSuggestedEditLinks }
            };
        }

        public static List<object> GetCommentLinks(SqlConnection cnn, IEnumerable<object> items)
        {
            return LookupIds(cnn, items, @"SELECT Id, Text FROM Comments WHERE Id IN ");
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

            var query = new StringBuilder()
                .Append(lookupSql)
                .Append("( ")
                .Append(list)
                .Append(" ) ");

            var linkMap = new Dictionary<long, object>();
            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandText = query.ToString();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var info = new MagicResult
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.IsDBNull(1) ? "unknown" : reader.GetString(1)
                        };

                        linkMap[info.Id] = info;
                    }
                }
            }


            foreach (object item in items)
            {
                if (!(item is int))
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