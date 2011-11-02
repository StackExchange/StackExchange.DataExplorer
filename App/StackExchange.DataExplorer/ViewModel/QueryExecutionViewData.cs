using System;
using System.Text;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public struct QueryExecutionViewData
    {
        private string name;
        private QueryVoting voting;

        public QueryVoting QueryVoting
        {
            get
            {
                return voting ?? new QueryVoting
                {
                    HasVoted = false,
                    TotalVotes = FavoriteCount,
                    ReadOnly = true
                };
            }

            set { voting = value; }
        }

        public string Name
        {
            get
            {
                return name ?? Query.SqlAsTitle(SQL);
            }
            set
            {
                name = value.IsNullOrEmpty() ? null : value;
            }
        }

        public string Url
        {
            get
            {
                return URLWithStub(null);
            }
        }

        public string URLWithStub(string stub)
        {
            // {0} - Site Name
            // {1} - Revision / Root ID
            // {2} - User ID
            // {3} - Slug
            string format = "/{0}/query/" + (stub != null ? stub + "/" : "");

            if (UseLatestLink && CreatorId != null)
            {
                format += "{2}/{1}{3}";
            }
            else
            {
                format += "{1}{3}";
            }

            return string.Format(format, new object[] {
                SiteName,
                Id,
                CreatorId ?? 0,
                name != null && stub == null ? "/" + name.URLFriendly() : ""
            });
        }

        public long RowNumber { get; set; }
        public bool Featured { get; set; }
        public bool Skipped { get; set; }
        public bool UseLatestLink { get; set; }
        public DateTime LastRun { get; set; }
        public int FavoriteCount { get; set; }
        public int? CreatorId { get; set; }
        public string CreatorLogin { get; set; }
        public int Views { get; set; }
        public string SiteName { get; set; }
        public string Description { get; set; }
        public string SQL { get; set; }
        public int Id { get; set; }
    }
}