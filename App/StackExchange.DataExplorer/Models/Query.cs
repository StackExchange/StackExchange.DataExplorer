using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models {
    public partial class Query {

        public string BodyWithoutComments { 
            get {
                ParsedQuery pq = new ParsedQuery(this.QueryBody, null);
                return pq.ExecutionSql;
            } 
        }
    }
}