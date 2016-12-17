using System;

namespace StackExchange.DataExplorer.Models
{
    public class Query
    {
        private static readonly int TITLE_LENGTH = 60;

        public int Id { get; set; }
        public Guid QueryHash { get; set; }
        public string QueryBody { get; set; }

        public string AsTitle()
        {
            return SqlAsTitle(QueryBody);
        }

        public static string SqlAsTitle(string sql)
        {
            var lines = sql.Split('\n');
            string title = "";

            for (var i = 0; i < lines.Length && title.Length < TITLE_LENGTH; ++i)
            {
                if (!lines[i].TrimStart().StartsWith("--"))
                {
                    title += lines[i].Trim() + " ";
                }
            }

            return title.TruncateWithEllipsis(TITLE_LENGTH);
        }
    }
}