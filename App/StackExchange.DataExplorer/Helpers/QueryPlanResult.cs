using System.Web;
using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    internal class QueryPlanResult : ActionResult
    {
        private readonly string plan;

        public QueryPlanResult(string plan)
        {
            this.plan = plan;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            HttpResponseBase response = context.HttpContext.Response;

            string attachment = "attachment; filename=ExecutionPlan.sqlplan";

            response.Clear();
            response.ContentType = "text/xml";
            response.AddHeader("content-disposition", attachment);
            response.AddHeader("Pragma", "public");
            response.Write(plan);
            response.Flush();
            response.Close();
        }
    }
}