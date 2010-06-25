using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Helpers
{
    public class QueryRunner
    {
        private const int QUERY_TIMEOUT = 120;
        private const int MAX_RESULTS = 2000;

        private static readonly Dictionary<Type, ResultColumnType> ColumnTypeMap = new Dictionary
            <Type, ResultColumnType>
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


        public static string GetCachedResults(ParsedQuery query, Site site)
        {
            CachedResult row = Current.DB.CachedResults
                .Where(result => result.QueryHash == query.ExecutionHash && result.SiteId == site.Id)
                .FirstOrDefault();
            string results = null;
            if (row != null)
            {
                results = row.Results;
            }
            return results;
        }

        public static void LogQueryExecution(User user, Site site, ParsedQuery parsedQuery)
        {
            if (user.IsAnonymous) return;

            Query query = Current.DB.Queries.FirstOrDefault(q => q.QueryHash == parsedQuery.Hash);
            if (query != null)
            {
                QueryExecution execution = Current.DB.QueryExecutions.FirstOrDefault(e => e.QueryId == query.Id);
                if (execution == null)
                {
                    execution = new QueryExecution
                                    {
                                        ExecutionCount = 1,
                                        FirstRun = DateTime.UtcNow,
                                        LastRun = DateTime.UtcNow,
                                        QueryId = query.Id,
                                        SiteId = site.Id,
                                        UserId = user.Id
                                    };
                    Current.DB.QueryExecutions.InsertOnSubmit(execution);
                }
                else
                {
                    execution.LastRun = DateTime.UtcNow;
                    execution.ExecutionCount++;
                    execution.SiteId = site.Id;
                }
                Current.DB.SubmitChanges();
            }
        }

        public static QueryResults ExecuteNonCached(ParsedQuery parsedQuery, Site site, User user)
        {
            DBContext db = Current.DB;
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

                    foreach (string batch in parsedQuery.ExecutionSqlBatches)
                    {
                        var command = new SqlCommand(batch, cnn);
                        command.CommandTimeout = QUERY_TIMEOUT;
                        PopulateResults(results, command, messages);
                    }
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

        private static void PopulateResults(QueryResults results, SqlCommand command, StringBuilder messages)
        {
            using (SqlDataReader reader = command.ExecuteReader())
            {
                do
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
                                var date = (DateTime) col;
                                col = date.ToString("yyyy-MM-dd hh:mm:ss");
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
                } while (reader.NextResult());
                command.Cancel();
                reader.Close();
            }
        }

        public static string GetJson(ParsedQuery parsedQuery, Site site, User user)
        {
            string json = null;
            DBContext db = Current.DB;

            json = GetCachedResults(parsedQuery, site);
            if (json != null)
            {
                // update the query if its not anon
                if (!user.IsAnonymous)
                {
                    Query query = db.Queries
                        .Where(q => q.QueryHash == parsedQuery.Hash)
                        .FirstOrDefault();
                    if (query != null)
                    {
                        query.Description = parsedQuery.Description;
                        query.Name = parsedQuery.Name;
                        query.QueryBody = parsedQuery.RawSql;
                        db.SubmitChanges();
                    }
                }
                return json;
            }

            QueryResults results = ExecuteNonCached(parsedQuery, site, user);

            json = results.ToJson();
            var cache = new CachedResult
                            {
                                QueryHash = parsedQuery.ExecutionHash,
                                SiteId = site.Id,
                                Results = json,
                                CreationDate = DateTime.UtcNow
                            };

            db.CachedResults.InsertOnSubmit(cache);
            db.SubmitChanges();

            return json;
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
                default:
                    break;
            }
        }

        private static Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>> GetMagicColumns()
        {
            var rval = new Dictionary<string, Func<SqlConnection, IEnumerable<object>, List<object>>>();
            rval["Post Link"] = GetPostLinks;
            rval["User Link"] = GetUserLinks;
            return rval;
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