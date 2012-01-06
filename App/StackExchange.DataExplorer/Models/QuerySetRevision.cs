using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public class QuerySetRevision
    {
        public int Id { get; set; }
        public int RevisionId { get; set; }
        public int QuerySetId { get; set; }
    }
}