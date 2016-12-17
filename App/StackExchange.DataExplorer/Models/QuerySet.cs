using System;
using System.Collections.Generic;

namespace StackExchange.DataExplorer.Models
{
    public class QuerySet
    {
        public int Id { get; set; }
        public int InitialRevisionId { get; set; }
        public int CurrentRevisionId { get; set; }
        public int? OwnerId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime LastActivity { get; set; }
        public int Votes { get; set; }
        public int Views { get; set; }
        public string OwnerIp { get; set; }

        // these are loaded via QueryUtil.LoadFullQuerySet

        public Revision InitialRevision { get; set; }
        public Revision CurrentRevision { get; set; }

        public List<Revision> Revisions { get; set; }

        public User Owner { get; set; }
    }
}