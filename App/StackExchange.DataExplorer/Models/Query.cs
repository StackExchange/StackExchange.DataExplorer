using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Helpers;
using System.Text;

namespace StackExchange.DataExplorer.Models {
    public partial class Query {

        public string BodyWithoutComments { 
            get {
                ParsedQuery pq = new ParsedQuery(this.QueryBody, null);
                return pq.ExecutionSql;
            } 
        }

        public void UpdateQueryBodyComment() {
            StringBuilder buffer = new StringBuilder();

            if (Name != null) {
                buffer.Append(ToComment(Name));
                buffer.Append("\n");
            }


            if (Description != null) {
                buffer.Append(ToComment(Description));
                buffer.Append("\n");
            }

            buffer.Append("\n");
            buffer.Append(BodyWithoutComments);

            QueryBody = buffer.ToString();
        }

        private string ToComment(string text) {

            if (string.IsNullOrEmpty(text)) return "";
            if (text != null) text = text.Trim();

            string rval = text.Replace("\r\n", "\n");
            rval = "-- " + rval;
            rval = rval.Replace("\n", "\n-- ");

            return rval;
        }
    }
}