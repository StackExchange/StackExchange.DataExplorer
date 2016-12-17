using System;

namespace StackExchange.DataExplorer.Models
{
    public class RevisionExecution
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public int SiteId { get; set; }
        public DateTime FirstRun { get; set; }
        public DateTime LastRun { get; set; }
        public int ExecutionCount { get; set; }
        public int RevisionId { get; set; }
    }
}