using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using System.Text;

namespace StackExchange.DataExplorer.Helpers {
    class CsvResult : ActionResult {
        QueryResults results;

        public CsvResult(string json) {
            results = QueryResults.FromJson(json);
        }

        public string RawCsv { 
            get {
                var sb = new StringBuilder();
                sb.AppendLine(String.Join(",", 
                    results.ResultSets[0].Columns.Select(col => col.Name).ToArray()));
                sb.Append( String.Join(Environment.NewLine,
                    results.ResultSets[0].Rows.Select(
                        row => String.Join(",", 
                            row.ToArray().Select(
                            c => c == null ? "" : c.ToString()
                            ).ToArray()
                          )
                       ).ToArray()
                 ));

                return sb.ToString();
            } 
        }

        public override void ExecuteResult(ControllerContext context) {
            var response = context.HttpContext.Response;

            string attachment = "attachment; filename=QueryResults.csv";

            response.Clear(); 
            response.ContentType = "text/csv";
            response.AddHeader("content-disposition", attachment);
            response.AddHeader("Pragma", "public");
            response.Write(RawCsv);  
            response.Flush();
            response.Close(); 
        }

    }
}