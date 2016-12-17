using System;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel
{
    public class QueryExecutionViewData
    {
        private string _name;
        private QuerySetVoting _voting;

        public QuerySetVoting QueryVoting
        {
            get
            {
                return _voting ?? new QuerySetVoting
                {
                    HasVoted = false,
                    TotalVotes = FavoriteCount,
                    ReadOnly = true
                };
            }

            set { _voting = value; }
        }

        public string Name
        {
            get { return _name ?? Query.SqlAsTitle(SQL); }
            set { _name = value.IsNullOrEmpty() ? null : value; }
        }

        public string Url
        {
            get
            {
                var slug = _name == null ? "" : "/" + _name.URLFriendly();
                if (RevisionId == null)
                {
                    return $"/{SiteName}/query/{QuerySetId}{slug}";
                }
                return $"/{SiteName}/revision/{QuerySetId}/{RevisionId}{slug}";
            }
        }


        public long RowNumber { get; set; }
        public bool Featured { get; set; }
        public bool Skipped { get; set; }
        public DateTime LastRun { get; set; }
        public int FavoriteCount { get; set; }
        public int? CreatorId { get; set; }
        public string CreatorLogin { get; set; }
        public int Views { get; set; }
        public string SiteName { get; set; }
        public string Description { get; set; }
        public string SQL { get; set; }
        public int? RevisionId { get; set; }
        public int QuerySetId { get; set; }
        public DateTime CreationDate { get; set; }
    }
}