using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models {
    public partial class Site {
        public SqlConnection GetConnection() {
            string connectionString = ConfigurationManager.ConnectionStrings["ReaderConnection"]
                   .ConnectionString
                   .Replace("!!DB!!", DatabaseName);

            return new SqlConnection(connectionString);
        }


        public void UpdateStats() {
            using (var cnn = GetConnection()) {
                cnn.Open();
                var cmd = new SqlCommand();
                cmd.Connection = cnn;
                cmd.CommandTimeout = 300;

                cmd.CommandText = "select count(*) from Posts where ParentId is null";
                this.TotalQuestions = (int)cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Posts where ParentId is not null";
                this.TotalAnswers = (int)cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Comments";
                this.TotalComments = (int)cmd.ExecuteScalar();

                cmd.CommandText = "select max(CreationDate) from Posts";
                this.LastPost = cmd.ExecuteScalar() as DateTime?;

                cmd.CommandText = "select count(*) from Users";
                this.TotalUsers = (int)cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Tags";
                this.TotalTags = (int)cmd.ExecuteScalar();
            }

            Current.DB.SubmitChanges();
        }

        public int? GuessUserId(User user) {

            if (user.IsAnonymous) return null;

            var cacheKey = "UserIdForSite" + this.Id.ToString() + "_" + user.Id.ToString();

            int? currentId = HttpContext.Current.Session[cacheKey] as int?; 
            if (currentId == null) {
                var id = FindUserId(user);
                if (id != null) {
                    HttpContext.Current.Cache[cacheKey] = id;
                }
            }

            currentId = HttpContext.Current.Cache[cacheKey] as int?; 
            if (currentId == null) {
                HttpContext.Current.Cache[cacheKey] = -1;
            }

            return currentId != -1 ? currentId : (int?)null;
        }

        private int? FindUserId(User user) {
            if (!user.IsAnonymous && user.Email != null) {
                var hash = Util.GravatarHash(user.Email);
                using (var cnn = GetConnection()) {
                    cnn.Open();
                    var cmd = cnn.CreateCommand();
                    cmd.CommandText = "select top 1 Id from Users where EmailHash = @EmailHash";
                    var p = cmd.Parameters.Add("@EmailHash", System.Data.SqlDbType.NVarChar);
                    p.Value = hash;
                    return (int?)cmd.ExecuteScalar();
                }
            }
            return null;
        }
    }
}