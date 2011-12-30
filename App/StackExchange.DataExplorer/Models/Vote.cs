using System;

namespace StackExchange.DataExplorer.Models
{
    public class Vote
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int VoteTypeId { get; set; }
        public DateTime CreationDate { get; set; }
        public int QuerySetId { get; set; }
    }
}