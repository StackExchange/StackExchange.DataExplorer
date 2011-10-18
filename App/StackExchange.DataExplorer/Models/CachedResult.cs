using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public partial class CachedResult
    {
        public string ExecutionPlan { get; set; }
        public string Messages { get; set; }
        public bool Truncated { get; set; }
    }
}