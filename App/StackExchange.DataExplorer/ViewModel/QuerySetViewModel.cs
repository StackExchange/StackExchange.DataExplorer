using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public class QuerySetViewModel 
    {
        public Revision CurrentRevision { get; set; }
        public QuerySet QuerySet { get; set; }
        public IEnumerable<Revision> Revisions { get; set; }
        public Site Site { get; set; }
    }
}
