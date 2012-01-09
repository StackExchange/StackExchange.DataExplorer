using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public struct RevisionHistory
    {
        public IEnumerable<Revision> History { get; set; }
        public Revision Current { get; set; }
        public string SiteName { get; set; }
    }
}