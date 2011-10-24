using System;

namespace StackExchange.DataExplorer.Models
{
    public class Revision
    {
        public int Id { get; set; }
        public int QueryId { get; set; }
        public int? OwnerId { get; set; }
        public string OwnerIP { get; set; }
        public bool IsFeature { get; set; }
        public DateTime CreationDate { get; set; }
        public Query Query { get; set; }
        public Metadata Metadata { get; set; }

        private int? rootId = null;
        public int? RootId {
            get
            {
                return rootId == null ? this.Id : this.rootId;
            }

            set {
                this.rootId = value;
            }
        }

        public bool IsRoot()
        {
            return this.rootId == null;
        }
    }
}