using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using StackExchange.DataExplorer.Models;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace StackExchange.DataExplorer.Helpers {

    public enum ResultColumnType {
        Default,
        Post,
        User,
        Number,
        Date, 
        Text
    }

    public class ResultColumnInfo {

        public ResultColumnInfo() {
            Type = ResultColumnType.Default;
        }

        public string Name { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public ResultColumnType Type { get; set; }
    }

    public class ResultSet {
        public ResultSet() {
            Columns = new List<ResultColumnInfo>();
            Rows = new List<List<object>>();
        }
        public List<ResultColumnInfo> Columns { get; set; }
        public List<List<object>> Rows { get; set; }
        // the position of the message when we started rendering this result set
        //  required so we can render in text
        public int MessagePosition { get; set; }
    } 

    public class QueryResults {

        public QueryResults() {
            ResultSets = new List<ResultSet>();
            FirstRun = DateTime.UtcNow.ToString("MMM %d yyyy");
            Messages = "";
        }

        public List<ResultSet> ResultSets { get; set; }

        public string Messages { get; set; }
        public string Url {get; set;}
        public int SiteId {get; set;}
        public string SiteName {get; set;}
        public int QueryId { get; set; }
        public int MaxResults { get; set; }
        public string FirstRun { get; set; }
        public bool Truncated { get; set; }
        public string Slug { get; set; }
        public bool TextOnly { get; set; }

        /// <summary>
        /// Execution time in Millisecs
        /// </summary>
        public long ExecutionTime { get; set; }

        public static JsonSerializerSettings GetSettings() {
            return new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };   
        }

        public static QueryResults FromJson(string json) {
            return JsonConvert.DeserializeObject<QueryResults>(json, GetSettings());
        }

        public QueryResults ToTextResults() {
            var results = new QueryResults();
            results.ExecutionTime = this.ExecutionTime;
            results.FirstRun = this.FirstRun;
            results.MaxResults = this.MaxResults;
            results.QueryId = this.QueryId;
            results.SiteId = this.SiteId;
            results.SiteName = this.SiteName;
            results.TextOnly = true;
            results.Truncated = this.Truncated;
            results.Url = this.Url;
            results.Slug = this.Slug;

            results.Messages = FormatTextResults(this.Messages, this.ResultSets);

            return results;
        }

        private static string FormatTextResults(string messages, List<ResultSet> resultSets) {
            StringBuilder buffer = new StringBuilder();
            int messagePosition = 0;
            int length;

            foreach (var resultSet in resultSets) {
                length = resultSet.MessagePosition - messagePosition;
                if (length > 0) {
                    buffer.Append(messages.Substring(messagePosition, length)); 
                }

                messagePosition = resultSet.MessagePosition;

                buffer.AppendLine(FormatResultSet(resultSet));
            }

            length = messages.Length - messagePosition;
            if (length > 0) {
                buffer.Append(messages.Substring(messagePosition, length));
            }

            return buffer.ToString();
        }

        const int MAX_TEXT_COLUMN_WIDTH = 512;

        private static string FormatResultSet(ResultSet resultSet) {
            StringBuilder buffer = new StringBuilder();
            var maxLengths = GetMaxLengths(resultSet);

            for (int j = 0; j < maxLengths.Count; j++) {
                maxLengths[j] = Math.Min(maxLengths[j], MAX_TEXT_COLUMN_WIDTH);
                buffer.Append(resultSet.Columns[j].Name.PadRight(maxLengths[j] + 1, ' '));
            }
            buffer.AppendLine();

            for (int k = 0; k < maxLengths.Count; k++) {
                buffer.Append("".PadRight(maxLengths[k], '-'));
                buffer.Append(" ");
            }

            buffer.AppendLine();

            foreach (var row in resultSet.Rows) {
                for (int i = 0; i < resultSet.Columns.Count; i++) {
                    var col = row[i];

                    string currentVal;
                    if (nativeTypes.Contains(resultSet.Columns[i].Type)) {
                        currentVal = (col ?? "null").ToString();
                    } else {
                        var data = col as JContainer;
                        if (data != null && data["title"] != null) {
                            currentVal = (data.Value<string>("title") ?? "null");
                        } else {
                            currentVal = "null";
                        }   
                    }
                    buffer.Append(currentVal.PadRight(maxLengths[i] + 1, ' '));
                }
                buffer.AppendLine();
            }

            return buffer.ToString();

        }

        private static readonly List<ResultColumnType> nativeTypes = new List<ResultColumnType>() { 
            ResultColumnType.Date,
            ResultColumnType.Default,
            ResultColumnType.Number,
            ResultColumnType.Text
        };

        private static List<int> GetMaxLengths(ResultSet resultSet) {
            var maxLengths = new List<int>();

            foreach (var colInfo in resultSet.Columns) {
                maxLengths.Add(colInfo.Name.Length);
            }

            foreach (var row in resultSet.Rows) {
                for (int i = 0; i < resultSet.Columns.Count; i++) {
                    var col = row[i];

                    int curLength;
                    if (nativeTypes.Contains(resultSet.Columns[i].Type)) {
                        curLength = col == null ? 4 : col.ToString().Length;
                    } else {
                        var data = col as JContainer;
                        if (data == null) {
                            curLength = 4;
                        } else {
                            curLength = (data.Value<string>("title") ?? "").Length;
                        }
                    }
                    maxLengths[i] = Math.Max(curLength, maxLengths[i]);
                }
            }
            return maxLengths;
        } 

        public string ToJson() {
           return
            JsonConvert.SerializeObject(
                this,
                Formatting.Indented,
                GetSettings()
            );
        }
    }
}