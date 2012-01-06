using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public class QueryViewerData
    {
        public Revision Revision { get; set; }
        public QuerySetVoting QuerySetVoting { get; set; }
    }
}