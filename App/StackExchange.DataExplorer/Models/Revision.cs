using System;

namespace StackExchange.DataExplorer.Models
{
    public class Revision
    {
        public int Id { get; set; }
        public int QueryId { get; set; }
        public int? OwnerId { get; set; }
        public string OwnerIP { get; set; }
        public DateTime CreationDate { get; set; }
        public int OriginalQuerySetId { get; set; }
        public Query Query { get; set; }
        public QuerySet QuerySet { get; set; }
        public User Owner { get; set; }
    }
}