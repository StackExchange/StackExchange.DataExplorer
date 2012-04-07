using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using StackExchange.DataExplorer.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StackExchange.DataExplorer.Helpers
{
    public class HelperTableCache
    {
        private static Dictionary<string, Dictionary<string, ResultSet>> cache = 
            new Dictionary<string, Dictionary<string, ResultSet>>();
        private static HelperTableCachePreferences preferences = null;
        private static Regex tableMatcher;

        public static HelperTableCachePreferences Preferences
        {
            get { return preferences;  }
            set {
                preferences = value;

                if (value != null)
                {
                    tableMatcher = new Regex(preferences.IncludePattern);
                }
            }
        }

        public static Dictionary<string, ResultSet> GetCache(Site site)
        {
            if (Preferences == null)
            {
                Refresh();

                if (Preferences == null)
                {
                    return null;
                }
            }

            if (!Preferences.PerSite)
            {
                return cache.First().Value;
            }
            else
            {
                return cache.ContainsKey(site.TinyName) ? cache[site.TinyName] : null;
            }
        }

        public static string GetCacheAsJson(Site site)
        {
            var cache = GetCache(site);

            if (cache != null)
            {
                return JsonConvert.SerializeObject(
                    cache,
                    Formatting.None,
                    new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
                );
            }

            return "{}";
        }

        public static void Refresh()
        {
            Preferences = AppSettings.HelperTableOptions;
            IEnumerable<Site> sites;

            cache.Clear();

            if (Preferences == null)
            {
                return;
            }

            if (!Preferences.PerSite)
            {
                sites = Current.DB.Query<Site>("SELECT TOP 1 * FROM Sites");
            }
            else
            {
                sites = Current.DB.Sites.All();
            }

            foreach (var site in sites)
            {
                IEnumerable<TableInfo> tables = site.GetTableInfos();
                cache[site.TinyName] = new Dictionary<string, ResultSet>();

                foreach (var table in tables) {
                    if (tableMatcher.IsMatch(table.Name))
                    {
                        using (SqlConnection connection = site.GetConnection()) {
                            connection.Open();
                            cache[site.TinyName][table.Name] = GetTableResults(connection, table);
                        }
                    }
                }
            }
        }

        private static ResultSet GetTableResults(SqlConnection connection, TableInfo table)
        {
            // We could probably refactor QueryRunner to expose this functionality
            // to us without having to recreate it here.
            var command = new SqlCommand(String.Format("SELECT * FROM {0} ORDER BY Id ASC", table.Name), connection);
            var resultSet = new ResultSet();

            using (var reader = command.ExecuteReader())
            {
                for (int i = 0; i < reader.FieldCount; ++i)
                {
                    var column = new ResultColumnInfo
                    {
                        Name = reader.GetName(i)
                    };

                    ResultColumnType type;

                    if (ResultColumnInfo.ColumnTypeMap.TryGetValue(reader.GetFieldType(i), out type))
                    {
                        column.Type = type;
                    }

                    resultSet.Columns.Add(column);
                }

                while (reader.Read())
                {
                    var row = new List<object>();

                    for (int i = 0; i < reader.FieldCount; ++i)
                    {
                        object value = reader.GetValue(i);

                        if (value is DateTime)
                        {
                            value = ((DateTime)value).ToJavascriptTime();
                        }

                        row.Add(value);
                    }

                    resultSet.Rows.Add(row);
                }
            }

            return resultSet;
        }
    }

    public class HelperTableCachePreferences
    {
        public HelperTableCachePreferences()
        {
            PerSite = false;
            IncludePattern = ".*Types$";
        }

        public bool PerSite { get; set; }
        public string IncludePattern { get; set; }
    }
}