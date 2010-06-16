using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Models;
using System.Data.SqlClient;
using System.Configuration;
using System.Web.Script.Serialization;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.ViewModel;
using System.Text.RegularExpressions;
using System.Text;

namespace StackExchange.DataExplorer.Controllers
{
    public class QueryController : StackOverflowController
    {
       
        [HttpPost]
        [Route("query/{siteId}")]
        public ActionResult Execute(string sql, int siteId, string resultsToText, int? savedQueryId)
        {

            var site = Current.DB.Sites.Where(s => s.Id == siteId).First();
            ActionResult rval;

            if (savedQueryId != null) {
                sql = Current.DB.SavedQueries.First(q => q.Id == savedQueryId.Value).Query.QueryBody;
            }

            var parsedQuery = new ParsedQuery(sql, Request.Params);

            try {
                if (!parsedQuery.AllParamsSet) {
                    throw new ApplicationException("All parameters must be set!");
                }

                var json = QueryRunner.GetJson(parsedQuery, site, CurrentUser);
                if (resultsToText == "true") {
                    json = QueryResults.FromJson(json).ToTextResults().ToJson();
                }

                rval = Content(json, "application/json");
                QueryRunner.LogQueryExecution(CurrentUser, site, parsedQuery);
            } catch (Exception e) {
                var result = new Dictionary<string, string>();
                var sqlException = e as SqlException;
                if (sqlException != null) {
                    result["error"] = sqlException.Message;
                } else {
                    result["error"] = e.ToString();
                }
                rval = Json( result ); 
            }

            return rval;
        }

        class ColumnInfo {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string DataType { get; set; }

            public void SetDataType(string name, int? length) {
                DataType = name;
                if (length != null) {
                    if (length == -1) {
                        DataType += " (max)";
                    } else {
                        DataType += " (" + length.ToString() +")";
                    }
                }
            }
        }



        [Route("{sitename}/query/new", RoutePriority.Low)]
        [Route("{sitename}/q/{query_id}", RoutePriority.Low)]
        [Route("{sitename}/q/{query_id}/{slug}", RoutePriority.Low)]

        [Route("{sitename}/qt/{query_id}", RoutePriority.Low)]
        [Route("{sitename}/csv/{query_id}", RoutePriority.Low)]

        [Route("{sitename}/qt/{query_id}/{slug}", RoutePriority.Low)]
        [Route("{sitename}/csv/{query_id}/{slug}", RoutePriority.Low)]

        public ActionResult New(string sitename, int? query_id, int? edit) {
            var db = Current.DB;

            string format = "";
            var searchLen = Math.Min(sitename.Length + 6, Request.Path.Length);
            if (Request.Path.IndexOf("/csv/", 0 , searchLen) > 0) { 
                format = "csv";
            }

            if (Request.Path.IndexOf("/qt/", 0, searchLen) > 0) {
                 format = "text";
            }
            SavedQuery savedQuery = null;

            if (edit != null) {
                SetHeader("Editing Query");
                ViewData["SavedQueryId"] = edit.Value;
                savedQuery = db.SavedQueries.FirstOrDefault(s => s.Id == edit.Value);

            } else {
                SetHeader("Compose Query");
            }

            
            var site = db.Sites.First(s => s.Name.ToLower() == sitename);
            this.Site = site;
            SelectMenuItem("Compose Query");

            ViewData["GuessedUserId"] = site.GuessUserId(CurrentUser);

            List<ColumnInfo> columns;

            using (var cnn = site.GetConnection()) {
                cnn.Open();
                var sql = @"
select TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.Columns
order by TABLE_NAME, ORDINAL_POSITION
";
                using (SqlCommand cmd = new SqlCommand(sql)) {
                    cmd.Connection = cnn;
                    using (var reader = cmd.ExecuteReader()) {
                        columns = new List<ColumnInfo>();
                        while (reader.Read()) {
                            var info = new ColumnInfo();
                            info.TableName = reader.GetString(0);
                            info.ColumnName = reader.GetString(1);
                            info.SetDataType(reader.GetString(2), reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3));
                            columns.Add(info);
                        }
                    }
                }

            }

            var tables = new List<TableInfo>();
            TableInfo tableInfo = null;

            foreach (var column in columns) {
                if (tableInfo == null || tableInfo.Name != column.TableName) {
                    tableInfo = new TableInfo();
                    tableInfo.Name = column.TableName;
                    tables.Add(tableInfo);
                }

                tableInfo.ColumnNames.Add(column.ColumnName);
                tableInfo.DataTypes.Add(column.DataType);
            }

            tables.Sort((l, r) =>
            {
                if (l.Name == "Posts") return -1;
                if (r.Name == "Posts") return 1;
                if (l.Name == "Users") return -1;
                if (r.Name == "Users") return 1;
                if (l.Name == "Comments") return -1;
                if (r.Name == "Comments") return 1;
                if (l.Name == "Badges") return -1;
                if (r.Name == "Badges") return 1;
                return l.Name.CompareTo(r.Name);
            });

            var serializer = new JavaScriptSerializer();
            ViewData["Tables"] = tables;

            ViewData["Sites"] = Current.DB.Sites.ToList();


            CachedResult cachedResults = null;

            if (query_id != null) {

                if (!IsSearchEngine()) {
                    QueryViewTracker.TrackQueryView(GetRemoteIP(), query_id.Value);
                }

                var query = db.Queries.Where(q => q.Id == query_id.Value).FirstOrDefault();
                if (query != null) {
                    if (savedQuery != null) {
                        query.Name = savedQuery.Title;
                        query.Description = savedQuery.Description;

                        StringBuilder buffer = new StringBuilder();

                        if (query.Name != null) {
                            buffer.Append(ToComment(query.Name));
                            buffer.Append("\n");
                        }

                        
                        if (query.Description != null) {
                            buffer.Append(ToComment(query.Description));
                            buffer.Append("\n");
                        }

                        buffer.Append("\n");
                        buffer.Append(query.BodyWithoutComments);

                        query.QueryBody = buffer.ToString();

                    }
                    ViewData["query"] = query;

                    cachedResults = GetCachedResults(query);
                    if (cachedResults != null) {
                        ViewData["cached_results"] = cachedResults;
                    }

                }
            }

            if (format == "csv") {
                return new CsvResult(cachedResults.Results);
            } else {
                if (format == "text") {

                    if (cachedResults != null && cachedResults.Results != null) {
                        cachedResults.Results = QueryResults.FromJson(cachedResults.Results).ToTextResults().ToJson();
                    }
                } 
                return View(site);
                
            }
        }

        private string ToComment(string text) {

            string rval = text.Replace("\r\n", "\n");
            rval = "-- " + rval;
            rval = rval.Replace("\n", "\n-- ");
            if (rval.Contains("\n--")) {
                rval = rval.Substring(0, rval.Length - 3);
            }
            return rval;
        }

    }
}
