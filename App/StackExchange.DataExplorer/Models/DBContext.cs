using System.Transactions;
using System.Configuration;
using System.Data.SqlClient;
using System.Data.Common;

namespace StackExchange.DataExplorer.Models
{
    /// These *MUST* be inside the Namespace! Fix for SP1 Linq to SQL Designer issue!
    
    partial class DBContext
    {
        /// <summary>
        /// Allows scoping of query performance data for logging.
        /// </summary>
        public string SessionName { get; set; }

        /// <summary>
        /// Allows unit tests to easily know which site this context was created for.
        /// </summary>
        public Site Site { get; set; }

        /// <summary>
        /// Answers a new TransactionScope with proper options for read heavy traffic.
        /// </summary>
        public static TransactionScope GetTransaction()
        {
            return new TransactionScope(TransactionScopeOption.Required,
                                        new TransactionOptions {IsolationLevel = IsolationLevel.ReadCommitted});
        }


        /// <summary>
        /// Answers a new DBContext for the current site.
        /// </summary>
        public static DBContext GetContext()
        {
            var cnnString = ConfigurationManager.ConnectionStrings["AppConnection"].ConnectionString;
            DbConnection connection = new SqlConnection(cnnString);


            if (Current.Profiler != null) 
            {
                connection = new StackExchange.MvcMiniProfiler.Data.ProfiledDbConnection(connection, Current.Profiler);    
            }

            return new DBContext(connection);
        }

        /// <summary>
        /// Ensures anonymous users aren't persisted to the database.
        /// </summary>
        /* public void InsertUser(User instance) {
            if (instance == null || instance.IsAnonymous)
                return;
            ExecuteDynamicInsert(instance);
        }*/
        public override string ToString()
        {
            return Site != null ? Site.Name : "unknown site";
        }
    }
}