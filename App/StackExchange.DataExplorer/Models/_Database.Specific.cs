using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public partial class Database
    {
        public Table<Site> Sites { get; private set; }
        public Table<User> Users { get; private set; }
        public Table<OpenIdWhiteList> OpenIdWhiteList { get; private set; }
        public Table<UserOpenId> UserOpenIds { get; private set; }
        public Table<Vote> Votes { get; private set; }
        public Table<BlackList> BlackList { get; private set; }
    }
}