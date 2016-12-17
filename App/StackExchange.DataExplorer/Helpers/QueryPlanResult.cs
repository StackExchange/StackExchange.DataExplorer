using System.Web.Mvc;

namespace StackExchange.DataExplorer.Helpers
{
    internal class QueryPlanResult : ActionResult
    {
        private readonly string _plan;

        public QueryPlanResult(string plan)
        {
            _plan = plan;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            var response = context.HttpContext.Response;
            
            response.Clear();
            response.ContentType = "text/xml";
            response.AddHeader("content-disposition", "attachment; filename=ExecutionPlan.sqlplan");
            response.AddHeader("Pragma", "public");
            response.Write(_plan);
            response.Flush();
            response.Close();
        }
    }
}