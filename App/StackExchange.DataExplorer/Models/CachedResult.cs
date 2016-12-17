using System;

namespace StackExchange.DataExplorer.Models
{
    public class CachedResult
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string Results { get; set; }
        public DateTime? CreationDate { get; set; }
        public Guid QueryHash { get; set; }
        
        public string ExecutionPlan { get; set; }
        public string Messages { get; set; }
        public bool Truncated { get; set; }
    }
}