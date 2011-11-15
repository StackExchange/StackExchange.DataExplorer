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
            int userId = user.IsAnonymous ? 0 : user.Id;

            if (revision.Parent != null)
            {
                if (revision.Parent.OwnerId != userId)
                {
                    relationship = "forked from";

                    if (revision.Parent != null)
                    {
                        relationship += " " + revision.Parent.Owner.Login + "'" + (!revision.Parent.Owner.Login.EndsWith("s") ? "s" : "");
                    }
                }
                else
                {
                    relationship = "created from your";
                }

                relationship += " revision " + revision.Parent.Id;
            }

            return "revision " + revision.Id + ", " + relationship;
        }
    }
}