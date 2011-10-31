using System;

namespace StackExchange.DataExplorer.Models
{
    public class Metadata
    {
        public int Id { get; set; }
        public int RevisionId { get; set; }
        public int? OwnerId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int LastQueryId { get; set; }
        public DateTime LastActivity { get; set; }
        public int Votes { get; set; }
        public int Views { get; set; }
    }
}