using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models {
    public partial class Site {

        class ColumnInfo {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string DataType { get; set; }

            public void SetDataType(string name, int? length) {
                DataType = name;
                if (length != null) {
                    if (length == -1) {
                        DataType += " (max)";
                    } else {
                        DataType += " (" + length.ToString() + ")";
                    }
                }
            }
        }

        public string ConnectionString
        {
            get
            {
                return ConfigurationManager.ConnectionStrings["ReaderConnection"]
                    .ConnectionString
                    .Replace("!!DB!!", DatabaseName);

            }

        }

        public SqlConnection GetConnection() {

            return new SqlConnection(ConnectionString);
        }

        public string ODataEndpoint
        {
           get
           {
            return "/" + Name.ToLower() + "/atom";
           }

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


        /// <summary>
        /// Get the sites schema
        /// </summary>
        /// <returns></returns>
        public List<TableInfo> GetTableInfos()
        {
            List<ColumnInfo> columns;
            var tables = new List<TableInfo>();


            using (var cnn = GetConnection()) {
                cnn.Open();
                var sql = @"
select TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.Columns
order by TABLE_NAME, ORDINAL_POSITION
";
                using (SqlCommand cmd = new SqlCommand(sql)) {
                    cmd.Connection = cnn;
                    using (var reader = cmd.ExecuteReader()) {
                        columns = new List<ColumnInfo>();
                        while (reader.Read()) {
                            var info = new ColumnInfo();
                            info.TableName = reader.GetString(0);
                            info.ColumnName = reader.GetString(1);
                            info.SetDataType(reader.GetString(2), reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3));
                            columns.Add(info);
                        }
                    }
                }

            }

            TableInfo tableInfo = null;

            foreach (var column in columns) {
                if (tableInfo == null || tableInfo.Name != column.TableName) {
                    tableInfo = new TableInfo();
                    tableInfo.Name = column.TableName;
                    tables.Add(tableInfo);
                }

                tableInfo.ColumnNames.Add(column.ColumnName);
                tableInfo.DataTypes.Add(column.DataType);
            }

            tables.Sort((l, r) =>
            {
                if (l.Name == "Posts") return -1;
                if (r.Name == "Posts") return 1;
                if (l.Name == "Users") return -1;
                if (r.Name == "Users") return 1;
                if (l.Name == "Comments") return -1;
                if (r.Name == "Comments") return 1;
                if (l.Name == "Badges") return -1;
                if (r.Name == "Badges") return 1;
                return l.Name.CompareTo(r.Name);
            });

            return tables;
        }

    }
}