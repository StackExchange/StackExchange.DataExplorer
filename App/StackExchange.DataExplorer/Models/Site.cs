using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using StackExchange.DataExplorer.Helpers;
using Dapper;
using System.Linq;

namespace StackExchange.DataExplorer.Models
{
    public class Site
    {
        public int Id { get; set; }
        public string TinyName { get; set; }
        public string  Name { get; set; }
        public string LongName { get; set; }
        public string Url { get; set; }
        public string ImageUrl { get; set; }
        public string IconUrl { get; set; }
        public string DatabaseName { get; set; }
        public string Tagline { get; set; }
        public string TagCss { get; set; }
        public int? TotalQuestions { get; set; }
        public int? TotalAnswers { get; set; }
        public int? TotalUsers { get; set; }
        public int? TotalComments { get; set; }
        public int? TotalTags { get; set; }
        public DateTime? LastPost { get; set; }
        public string ImageBackgroundColor { get; set; }
        public string ConnectionStringOverride { get; set; }
        public int? ParentId { get; set; }
        public string BadgeIconUrl { get; set; }
        // above props are columns on dbo.Sites

        private Site _relatedSite;
        private bool _checkedRelatedSite;

        public Site RelatedSite
        {
            get
            {
                if (!_checkedRelatedSite && _relatedSite == null)
                {
                    if (ParentId == null)
                    {
                        _relatedSite = Current.DB.Query<Site>("SELECT * FROM Sites WHERE ParentId = @id", new { Id }).FirstOrDefault();
                    }
                    else
                    {
                        _relatedSite = Current.DB.Query<Site>("SELECT * FROM Sites WHERE Id = @parentId", new { parentId = ParentId.Value }).FirstOrDefault();
                    }

                    // We could just query for the related site when getting this site and avoid the need
                    // for this variable, but we don't always need related site...so I'm not sure what's
                    // "less silly"...
                    _checkedRelatedSite = true;
                }

                return _relatedSite;
            }
            private set {}
        }

        public string ConnectionString => 
            UseConnectionStringOverride ? ConnectionStringOverride : ConnectionStringWebConfig;

        private string ConnectionStringWebConfig =>
            ConfigurationManager.ConnectionStrings["ReaderConnection"]
                .ConnectionString
                .Replace("!!DB!!", DatabaseName);

        private bool UseConnectionStringOverride => ConnectionStringOverride.HasValue();

        public string ImageCss => ImageBackgroundColor != null ? "background-color: #" + ImageBackgroundColor : "";

        public string ODataEndpoint => "/" + TinyName.ToLower() + "/atom";

        public SqlConnection GetConnection(int maxPoolSize)
        {
            // TODO: do we even need this method any longer? are we still supporting about odata?
            var cs = ConnectionString + (UseConnectionStringOverride ? "" : ((ConnectionString.EndsWith(";") ? "" : ";") + string.Format("Max Pool Size={0};",maxPoolSize)));
            return new SqlConnection(cs);
        }

        public SqlConnection GetOpenConnection()
        {
            var cnn = new SqlConnection(ConnectionString);
            cnn.Open();
            if (AppSettings.FetchDataInReadUncommitted) { cnn.Execute("set transaction isolation level read uncommitted"); }
            return cnn;
        }

        public bool SharesUsers(Site site)
        {
            var shares = false;

            if (Url.StartsWith("http://meta.") && Url != "http://meta.stackexchange.com")
            {
                shares = Url.Substring("http://meta.".Length) == site.Url.Substring("http://".Length);
            }
            else if (site.Url.StartsWith("http://meta.") && site.Url != "http://meta.stackexchange.com")
            {
                shares = site.Url.Substring("http://meta.".Length) == Url.Substring("http://".Length);
            }

            return shares;
        }

        public void UpdateStats()
        {
            using (var cnn = GetOpenConnection())
            using (var cmd = new SqlCommand())
            {
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

            Current.DB.Sites.Update(Id, new {TotalQuestions, TotalAnswers, TotalComments, LastPost, TotalUsers, TotalTags });
        }

        public int? GuessUserId(User user)
        {
            if (user.IsAnonymous || !AppSettings.GuessUserId) return null;

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
                using (var cnn = GetOpenConnection())
                {
                    string hash = Util.GravatarHash(user.Email);
                    try
                    {
                        return cnn.Query<int?>("select top 1 Id from Users where EmailHash = @hash order by Reputation desc", new {hash}).FirstOrDefault();
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
            using (var cnn = GetOpenConnection())
            {
                const string sql = @"
select TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH from INFORMATION_SCHEMA.COLUMNS
order by TABLE_NAME, ORDINAL_POSITION
";
                using (var cmd = new SqlCommand(sql))
                {
                    cmd.Connection = cnn;
                    using (var reader = cmd.ExecuteReader())
                    {
                        columns = new List<ColumnInfo>();
                        while (reader.Read())
                        {
                            var info = new ColumnInfo
                            {
                                TableName = reader.GetString(0),
                                ColumnName = reader.GetString(1)
                            };
                            info.SetDataType(reader.GetString(2), reader.IsDBNull(3) ? null : (int?) reader.GetInt32(3));
                            columns.Add(info);
                        }
                    }
                }
            }

            TableInfo tableInfo = null;

            foreach (var column in columns)
            {
                if (tableInfo == null || tableInfo.Name != column.TableName)
                {
                    tableInfo = new TableInfo {Name = column.TableName};
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

        public SiteInfo SiteInfo => new SiteInfo { Id = Id, Name = LongName, Url = Url };
    }
}