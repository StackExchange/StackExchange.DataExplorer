using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public class DataExplorerDatabase : Dapper.Database<DataExplorerDatabase>
    {
        public Table<Site> Sites { get; private set; }
        public Table<User> Users { get; private set; }
        public Table<OpenIdWhiteList> OpenIdWhiteList { get; private set; }
        public Table<UserAuthClaim> UserAuthClaims { get; private set; }
        public Table<Vote> Votes { get; private set; }
        public Table<BlackList> BlackList { get; private set; }
        public Table<QuerySet> QuerySets { get; private set; }
        public Table<AppSetting> AppSettings { get; private set; }
        public Table<Revision> Revisions { get; private set; }
        public Table<Query> Queries { get; private set; }
        public Table<QuerySetRevision> QuerySetRevisions { get; private set; }
    }
}