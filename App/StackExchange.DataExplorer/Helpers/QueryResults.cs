using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Xml.Xsl;
using System.Web;

namespace StackExchange.DataExplorer.Helpers
{
    public enum ResultColumnType
    {
        Default,
        Post,
        User,
        Number,
        Date,
        Text
    }

    public class ResultColumnInfo
    {
        public ResultColumnInfo()
        {
            Type = ResultColumnType.Default;
        }

        public string Name { get; set; }

        [JsonConverter(typeof (StringEnumConverter))]
        public ResultColumnType Type { get; set; }
    }

    public class ResultSet
    {
        public ResultSet()
        {
            Columns = new List<ResultColumnInfo>();
            Rows = new List<List<object>>();
        }

        public List<ResultColumnInfo> Columns { get; set; }
        public List<List<object>> Rows { get; set; }
        // the position of the message when we started rendering this result set
        //  required so we can render in text
        public int MessagePosition { get; set; }
    }

    public class QueryResults
    {
        private const int MAX_TEXT_COLUMN_WIDTH = 512;

        private static readonly List<ResultColumnType> nativeTypes = new List<ResultColumnType>
                                                                         {
                                                                             ResultColumnType.Date,
                                                                             ResultColumnType.Default,
                                                                             ResultColumnType.Number,
                                                                             ResultColumnType.Text
                                                                         };

        public QueryResults()
        {
            ResultSets = new List<ResultSet>();
            this.ExecutionPlans = new List<string>();
            FirstRun = DateTime.UtcNow.ToString("MMM %d yyyy");
            Messages = "";
        }

        public List<ResultSet> ResultSets { get; set; }

        /// <summary>
        /// Gets and sets a list of query execution plans associated with the query results.
        /// </summary>
        public List<string> ExecutionPlans { get; set; }

        public bool MultiSite { get; set; }
        public bool ExcludeMetas { get; set; }
        public string Messages { get; set; }
        public string Url { get; set; }
        public int SiteId { get; set; }
        public string SiteName { get; set; }
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

        public static JsonSerializerSettings GetSettings()
        {
            return new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};
        }

        public static QueryResults FromJson(string json)
        {
            return JsonConvert.DeserializeObject<QueryResults>(json, GetSettings());
        }

        public QueryResults ToTextResults()
        {
            var results = new QueryResults();
            results.ExecutionTime = ExecutionTime;
            results.FirstRun = FirstRun;
            results.MaxResults = MaxResults;
            results.QueryId = QueryId;
            results.SiteId = SiteId;
            results.SiteName = SiteName;
            results.TextOnly = true;
            results.Truncated = Truncated;
            results.Url = Url;
            results.Slug = Slug;
            results.MultiSite = MultiSite;
            results.ExcludeMetas = ExcludeMetas;

            results.Messages = FormatTextResults(Messages, ResultSets);

            return results;
        }

        public QueryResults TransformQueryPlan()
        {
            var returnValue = new QueryResults();
            returnValue.ExecutionTime = this.ExecutionTime;
            returnValue.FirstRun = this.FirstRun;
            returnValue.MaxResults = this.MaxResults;
            returnValue.QueryId = this.QueryId;
            returnValue.SiteId = this.SiteId;
            returnValue.SiteName = this.SiteName;
            returnValue.TextOnly = this.TextOnly;
            returnValue.Truncated = this.Truncated;
            returnValue.Url = this.Url;
            returnValue.Slug = this.Slug;
            returnValue.MultiSite = this.MultiSite;
            returnValue.ExcludeMetas = this.ExcludeMetas;
            returnValue.ResultSets = this.ResultSets;

            returnValue.ExecutionPlans = this.ExecutionPlans.ConvertAll<string>(plan => TransformPlan(plan));

            return returnValue;
        }

        private static string FormatTextResults(string messages, List<ResultSet> resultSets)
        {
            var buffer = new StringBuilder();
            int messagePosition = 0;
            int length;

            foreach (ResultSet resultSet in resultSets)
            {
                length = resultSet.MessagePosition - messagePosition;
                if (length > 0)
                {
                    buffer.Append(messages.Substring(messagePosition, length));
                }

                messagePosition = resultSet.MessagePosition;

                buffer.AppendLine(FormatResultSet(resultSet));
            }

            length = messages.Length - messagePosition;
            if (length > 0)
            {
                buffer.Append(messages.Substring(messagePosition, length));
            }

            return buffer.ToString();
        }

        /// <summary>
        /// Transforms an xml execution plan into html.
        /// </summary>
        /// <param name="plan">Xml query plan as a string.</param>
        /// <returns>Html query plan as a string.</returns>
        private static string TransformPlan(string plan)
        {
            if (string.IsNullOrEmpty(plan))
            {
                return null;
            }

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(plan);

            XslCompiledTransform t = new XslCompiledTransform(true);
            t.Load(HttpContext.Current.Server.MapPath(@"~/Content/qp/qp.xslt"));

            StringBuilder returnValue = new StringBuilder();
            using (var writer = System.Xml.XmlWriter.Create(returnValue, t.OutputSettings))
            {
                t.Transform(doc, writer);
            }
            return returnValue.ToString();
        }

        private static string FormatResultSet(ResultSet resultSet)
        {
            var buffer = new StringBuilder();
            List<int> maxLengths = GetMaxLengths(resultSet);

            for (int j = 0; j < maxLengths.Count; j++)
            {
                maxLengths[j] = Math.Min(maxLengths[j], MAX_TEXT_COLUMN_WIDTH);
                buffer.Append(resultSet.Columns[j].Name.PadRight(maxLengths[j] + 1, ' '));
            }
            buffer.AppendLine();

            for (int k = 0; k < maxLengths.Count; k++)
            {
                buffer.Append("".PadRight(maxLengths[k], '-'));
                buffer.Append(" ");
            }

            buffer.AppendLine();

            foreach (var row in resultSet.Rows)
            {
                for (int i = 0; i < resultSet.Columns.Count; i++)
                {
                    object col = row[i];

                    string currentVal;
                    if (nativeTypes.Contains(resultSet.Columns[i].Type))
                    {
                        currentVal = (col ?? "null").ToString();
                    }
                    else
                    {
                        var data = col as JContainer;
                        if (data != null && data["title"] != null)
                        {
                            currentVal = (data.Value<string>("title") ?? "null");
                        }
                        else
                        {
                            currentVal = "null";
                        }
                    }
                    buffer.Append(currentVal.PadRight(maxLengths[i] + 1, ' '));
                }
                buffer.AppendLine();
            }

            return buffer.ToString();
        }

        private static List<int> GetMaxLengths(ResultSet resultSet)
        {
            var maxLengths = new List<int>();

            foreach (ResultColumnInfo colInfo in resultSet.Columns)
            {
                maxLengths.Add(colInfo.Name.Length);
            }

            foreach (var row in resultSet.Rows)
            {
                for (int i = 0; i < resultSet.Columns.Count; i++)
                {
                    object col = row[i];

                    int curLength;
                    if (nativeTypes.Contains(resultSet.Columns[i].Type))
                    {
                        curLength = col == null ? 4 : col.ToString().Length;
                    }
                    else
                    {
                        var data = col as JContainer;
                        if (data == null)
                        {
                            curLength = 4;
                        }
                        else
                        {
                            curLength = (data.Value<string>("title") ?? "").Length;
                        }
                    }
                    maxLengths[i] = Math.Max(curLength, maxLengths[i]);
                }
            }
            return maxLengths;
        }

        public string ToJson()
        {
            return
                JsonConvert.SerializeObject(
                    this,
                    Formatting.Indented,
                    GetSettings()
                    );
        }
    }
}