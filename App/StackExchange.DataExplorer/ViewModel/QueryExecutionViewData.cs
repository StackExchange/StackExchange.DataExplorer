using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.ViewModel {
    public struct QueryExecutionViewData {

        public bool Featured { get; set; }
        public bool Skipped { get; set; }

        public DateTime LastRun { get; set; }
        public string SiteName { get; set; }

        public int FavoriteCount { get; set; }

        public User Creator { get; set; }

        public int Views { get; set; }

        private string name;
        public string Name { 
            get {
                return name ?? ShortSqlExcerpt;         
            }
            set {
                name = value;
            }
        }

        private string description;
        public string Description { 
            get {
                return description ?? SQL; 
            }
            set {
                description = value;
            }
        }

        public string SQL { get; set; }
        public int Id { get; set; }

        public string Url {
            get {
                var prefix = UrlPrefix ?? "s";
                return "/" + SiteName + "/" + prefix +"/" + Id.ToString() + "/" + this.Name.URLFriendly();
            }
        }

        private string ShortSqlExcerpt {
            get {
                var str = SQL;
                if (str.Length > 80) {
                    str = str.Substring(0, 80);
                    str += " ...";
                }
                return str.Replace("\n", " ").Replace("\r", "");
            }
        }

        public string UrlPrefix { get; set; }
    }
}