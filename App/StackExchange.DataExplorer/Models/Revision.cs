using System;

namespace StackExchange.DataExplorer.Models
{
    public class Revision
    {
        public int Id { get; set; }
        public int QueryId { get; set; }
        public int RootId { get; set; }
        public int OwnerId { get; set; }
        public string OwnerIP { get; set; }
        public bool IsFeature { get; set; }
        public DateTime CreationDate { get; set; }
        public Query Query { get; set; }
        public Metadata Metadata { get; set; }
    }
}