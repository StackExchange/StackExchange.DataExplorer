using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models
{
    public partial class Site
    {
        public string ConnectionString
        {
            get
            {
                return ConfigurationManager.ConnectionStrings["ReaderConnection"]
                    .ConnectionString
                    .Replace("!!DB!!", DatabaseName);
            }
        }

        public string ODataEndpoint
        {
            get { return "/" + Name.ToLower() + "/atom"; }
        }

        public SqlConnection GetConnection(int maxPoolSize)
        {
            return new SqlConnection(ConnectionString + string.Format("Max Pool Size={0};",maxPoolSize));
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public void UpdateStats()
        {
            using (SqlConnection cnn = GetConnection())
            using( var cmd = new SqlCommand())
            {
                cnn.Open();
               
                cmd.Connection = cnn;
                cmd.CommandTimeout = 300;

                cmd.CommandText = "select count(*) from Posts where ParentId is null";
                TotalQuestions = (int) cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Posts where ParentId is not null";
                TotalAnswers = (int) cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Comments";
                TotalComments = (int) cmd.ExecuteScalar();

                cmd.CommandText = "select max(CreationDate) from Posts";
                LastPost = cmd.ExecuteScalar() as DateTime?;

                cmd.CommandText = "select count(*) from Users";
                TotalUsers = (int) cmd.ExecuteScalar();

                cmd.CommandText = "select count(*) from Tags";
                TotalTags = (int) cmd.ExecuteScalar();
            }

            Current.DB.SubmitChanges();
        }

        public int? GuessUserId(User user)
        {
            if (user.IsAnonymous) return null;

            string cacheKey = "UserIdForSite" + Id + "_" + user.Id;

            var currentId = HttpContext.Current.Session[cacheKey] as int?;
            if (currentId == null)
            {
                int? id = FindUserId(user);
                if (id != null)
                {
                    HttpContext.Current.Cache[cacheKey] = id;
                }
            }

            currentId = HttpContext.Current.Cache[cacheKey] as int?;
            if (currentId == null)
            {
                HttpContext.Current.Cache[cacheKey] = -1;
            }

            return currentId != -1 ? currentId : null;
        }

        private int? FindUserId(User user)
        {
            if (!user.IsAnonymous && user.Email != null)
            {
                string hash = Util.GravatarHash(user.Email);
                using (SqlConnection cnn = GetConnection())
                {
                    cnn.Open();
                    SqlCommand cmd = cnn.CreateCommand();
                    cmd.CommandText = "select top 1 Id from Users where EmailHash = @EmailHash";
                    SqlParameter p = cmd.Parameters.Add("@EmailHash", SqlDbType.NVarChar);
                    p.Value = hash;
                    try
                    {
                        return (int?)cmd.ExecuteScalar();
                    }
                    catch
                    { 
                        // allow this to fail, its not critical
                    }
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


            using (SqlConnection cnn = GetConnection())
            {
                cnn.Open();
                string sql =
                    @"
select TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.COLUMNS
order by TABLE_NAME, ORDINAL_POSITION
";
                using (var cmd = new SqlCommand(sql))
                {
                    cmd.Connection = cnn;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        columns = new List<ColumnInfo>();
                        while (reader.Read())
                        {
                            var info = new ColumnInfo();
                            info.TableName = reader.GetString(0);
                            info.ColumnName = reader.GetString(1);
                            info.SetDataType(reader.GetString(2), reader.IsDBNull(3) ? null : (int?) reader.GetInt32(3));
                            columns.Add(info);
                        }
                    }
                }
            }

            TableInfo tableInfo = null;

            foreach (ColumnInfo column in columns)
            {
                if (tableInfo == null || tableInfo.Name != column.TableName)
                {
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

        #region Nested type: ColumnInfo

        private class ColumnInfo
        {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string DataType { get; set; }

            public void SetDataType(string name, int? length)
            {
                DataType = name;
                if (length != null)
                {
                    if (length == -1)
                    {
                        DataType += " (max)";
                    }
                    else
                    {
                        DataType += " (" + length + ")";
                    }
                }
            }
        }

        #endregion
    }
}