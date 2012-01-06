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

    public static class RevisionHistoryExtensions
    {
        public static string ExplainOrigin(this Revision revision, User user)
        {
            string relationship = "initial version";

            return "revision " + revision.Id + ", " + relationship;
        }
    }
}