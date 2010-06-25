using System.Text;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models
{
    public partial class Query
    {
        public string BodyWithoutComments
        {
            get
            {
                var pq = new ParsedQuery(QueryBody, null);
                return pq.ExecutionSql;
            }
        }

        public void UpdateQueryBodyComment()
        {
            var buffer = new StringBuilder();

            if (Name != null)
            {
                buffer.Append(ToComment(Name));
                buffer.Append("\n");
            }


            if (Description != null)
            {
                buffer.Append(ToComment(Description));
                buffer.Append("\n");
            }

            buffer.Append("\n");
            buffer.Append(BodyWithoutComments);

            QueryBody = buffer.ToString();
        }

        private string ToComment(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text != null) text = text.Trim();

            string rval = text.Replace("\r\n", "\n");
            rval = "-- " + rval;
            rval = rval.Replace("\n", "\n-- ");

            return rval;
        }
    }
}