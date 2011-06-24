using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    internal class CsvResult : ActionResult
    {
        private readonly QueryResults results;

        public CsvResult(string json)
        {
            results = QueryResults.FromJson(json);
        }

        public string RawCsv
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(String.Join(",",
                                          results.ResultSets[0].Columns.Select(col => col.Name).ToArray()));
                sb.Append(String.Join(Environment.NewLine,
                                      results.ResultSets[0].Rows.Select(
                                          row => "\"" + String.Join("\",\"",
                                                             row.ToArray().Select(
                                                                 c => c == null ? "" : c.ToString().Replace("\"", "\"\"")
                                                                 ).ToArray()
                                                     ) + "\""
                                          ).ToArray()
                              ));

                return sb.ToString();
            }
        }

        public override void ExecuteResult(ControllerContext context)
        {
            HttpResponseBase response = context.HttpContext.Response;

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