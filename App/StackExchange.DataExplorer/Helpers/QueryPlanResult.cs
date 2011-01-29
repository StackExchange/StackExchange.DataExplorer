using System.Web;
using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    internal class QueryPlanResult : ActionResult
    {
        private readonly QueryResults results;

        public QueryPlanResult(string json)
        {
            results = QueryResults.FromJson(json);
        }

        private string PanlXml
        {
            get
            {
                // TODO: This only returns the first plan
                return this.results.ExecutionPlans.Count == 0 ? null : results.ExecutionPlans[0];
            }
        }

        public override void ExecuteResult(ControllerContext context)
        {
            HttpResponseBase response = context.HttpContext.Response;

            string attachment = "attachment; filename=ExecutionPlan.sqlplan";

            response.Clear();
            response.ContentType = "text/xml";
            response.AddHeader("content-disposition", attachment);
            response.AddHeader("Pragma", "public");
            response.Write(this.PanlXml);
            response.Flush();
            response.Close();
        }
    }
}