using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    internal class CsvResult : ActionResult
    {
        private readonly List<ResultSet> _resultSets;

        public CsvResult(List<ResultSet> results)
        {
            _resultSets = results;
        }

        public string RawCsv
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", _resultSets[0].Columns.Select(col => col.Name).ToArray()));
                sb.Append(
                    string.Join(
                        Environment.NewLine,
                        _resultSets[0].Rows.Select(
                            row =>
                            {
                                var i = 0;
                                return "\"" + string.Join("\",\"", row.Select(c => Prepare(c, i++)).ToArray()) + "\"";
                            }
                        ).ToArray()
                    )
                );

                return sb.ToString();
            }
        }

        private string Prepare(object value, int index)
        {
            if (value == null)
            {
                return "";
            }

            if (_resultSets[0].Columns[index].Type == ResultColumnType.Date)
            {
                return Util.FromJavaScriptTime((long)value).ToString("yyyy-MM-dd HH:mm:ss");
            }

            var siteInfo = value as SiteInfo; 
            if (siteInfo != null)
            {

                return siteInfo.Name;
            }

            return value.ToString().Replace("\"", "\"\"");
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;

            response.Clear();
            response.ContentType = "text/csv";
            response.AddHeader("content-disposition", "attachment; filename=QueryResults.csv");
            response.AddHeader("content-length", Encoding.UTF8.GetByteCount(RawCsv).ToString());
            response.AddHeader("Pragma", "public");
            response.Write(RawCsv);
            response.Flush();
            response.Close();
        }
    }
}