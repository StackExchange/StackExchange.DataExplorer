using System;
using System.Text;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public struct QueryExecutionViewData
    {
        private string description;
        private string name;

        public QueryVoting QueryVoting
        {
            get
            {
                return new QueryVoting
                           {
                               HasVoted = false,
                               TotalVotes = FavoriteCount,
                               ReadOnly = true
                           };
            }
        }

        public bool Featured { get; set; }
        public bool Skipped { get; set; }
        public bool UseLatestLink { get; set; }

        public DateTime LastRun { get; set; }
        public string SiteName { get; set; }

        public int FavoriteCount { get; set; }

        public User Creator { get; set; }

        public int Views { get; set; }

        public string Name
        {
            get { return name ?? ShortSqlExcerpt; }
            set { name = value; }
        }

        public string Description
        {
            get { return description ?? StripInitialComments(SQL); }
            set { description = value; }
        }

        public string SQL { get; set; }
        public int Id { get; set; }

        public string Url
        {
            get
            {
                // {0} - Site Name
                // {1} - Revision / Root ID
                // {2} - User ID
                // {3} - Slug
                string format = "/{0}/query/";

                if (UseLatestLink && Creator != null)
                {
                    format += "{2}/{1}/{3}";
                }
                else
                {
                    format += "{1}/{3}";
                }

                return string.Format(format, new object[] {
                    SiteName,
                    Id,
                    Creator != null ? Creator.Id : 0,
                    Name.URLFriendly()
                });
            }
        }

        private string ShortSqlExcerpt
        {
            get
            {
                string str = StripInitialComments(SQL);
                if (str.Length > 80)
                {
                    str = str.Substring(0, 80);
                    str += " ...";
                }
                return str.Replace("\n", " ").Replace("\r", "");
            }
        }

        public string UrlPrefix { get; set; }

        private string StripInitialComments(string str)
        {
            var sb = new StringBuilder();

            bool atStart = true;
            foreach (string line in str.Split('\n'))
            {
                if (atStart && (line.StartsWith("--") || line.Trim() == ""))
                {
                    continue;
                }
                atStart = false;
                sb.AppendLine(line);
            }


            return sb.ToString();
        }
    }
}