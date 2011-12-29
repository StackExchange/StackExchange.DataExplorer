using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;
using System.Data;
using Dapper;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Data.Common;
using MvcMiniProfiler;

namespace StackExchange.DataExplorer.Models
{
    public partial class Database : IDisposable
    {
        public class Table<T>
        {
            Database database;
            string tableName;

            public Table(Database database)
            {
                this.database = database;
            }

            public string TableName 
            { 
                get 
                {
                    tableName = tableName ?? database.DetermineTableName<T>();
                    return tableName;
                } 
            }

            public int? Insert(dynamic data)
            {
                var o = (object)data;
                List<string> paramNames = GetParamNames(o);

                string cols = string.Join(",", paramNames);
                string cols_params = string.Join(",", paramNames.Select(p => "@" + p));
                var sql = "set nocount on insert " + TableName + " (" + cols + ") values (" + cols_params + ") select cast(scope_identity() as int)";

                return database.Query<int?>(sql, o).Single();
            }

            public int Update(int id, dynamic data)
            {
                List<string> paramNames = GetParamNames(data);

                var builder = new StringBuilder();
                builder.Append("update [").Append(TableName).Append("] set ");
                builder.AppendLine(string.Join(",", paramNames.Where(n => n != "Id").Select(p => p + "= @" + p)));
                builder.Append("where Id = @Id");

                DynamicParameters parameters = new DynamicParameters(data);
                parameters.Add("Id", id);

                return database.Execute(builder.ToString(), parameters);
            }

            public bool Delete(int id)
            {
                return database.Execute("delete " + TableName + " where Id = @id", new { id }) > 0;
            }

            public T Get(int id)
            {
                return database.Query<T>("select * from " + TableName + " where id = @id", new { id }).FirstOrDefault();
            }

            public IEnumerable<T> All()
            {
                return database.Query<T>("select * from " + TableName);
            }

            static ConcurrentDictionary<Type, List<string>> paramNameCache = new ConcurrentDictionary<Type, List<string>>();
            private static List<string> GetParamNames(object o)
            {
                List<string> paramNames;
                if (!paramNameCache.TryGetValue(o.GetType(), out paramNames))
                {
                    paramNames = new List<string>();
                    foreach (var prop in o.GetType().GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public))
                    {
                        paramNames.Add(prop.Name);
                    }
                    paramNameCache[o.GetType()] = paramNames;
                }
                return paramNames;
            }
        } 

        DbConnection connection;
        int commandTimeout;
        DbTransaction transaction;

        private static Action<Database> tableConstructor; 

        public Database(DbConnection connection, int commandTimeout)
        {
            this.connection = connection;
            this.commandTimeout = commandTimeout;
            if (tableConstructor == null)
            {
                tableConstructor = CreateTableConstructor(); 
            }
            
            // this takes 0.1ms in dev, it could be sped up to close to nothing by baking a method.
            tableConstructor(this);
        }

        public Action<Database> CreateTableConstructor()
        {
            var setters = GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Table<>))
                .Select(p => Tuple.Create(p.GetSetMethod(true), p.PropertyType.GetConstructor(new Type[] { typeof(Database) })));

            return (db) => 
            {
                foreach (var setter in setters)
                {
                    setter.Item1.Invoke(db, new object[] { setter.Item2.Invoke(new object[] {db}) });
                }
            };
        }

        static ConcurrentDictionary<Type, string> tableNameMap = new ConcurrentDictionary<Type, string>();
        private string DetermineTableName<T>()
        {
            string name;

            if (!tableNameMap.TryGetValue(typeof(T), out name))
            {
                name = typeof(T).Name;
                if (!TableExists(name))
                {
                    // the most stupid pluralizer ever
                    if (TableExists(name + "s"))
                    {
                        name = name + "s";
                    }
                    else
                    {
                        name = name + "es";
                    }
                }

                tableNameMap[typeof(T)] = name;
            }
            return name;
        }

        private bool TableExists(string name)
        {
            return connection.Query("select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @name", new { name }).Count() == 1;
        }

        public int Execute(string sql, dynamic param = null)
        {
            return SqlMapper.Execute(connection, sql, param as object, transaction, commandTimeout: this.commandTimeout);
        }

        public IEnumerable<T> Query<T>(string sql, dynamic param = null, bool buffered = true)
        {
           return SqlMapper.Query<T>(connection, sql, param as object, transaction, buffered, commandTimeout);
        }

        public IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(connection, sql, map, param as object, transaction, buffered, splitOn);
        }

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(connection, sql, map, param as object, transaction, buffered, splitOn);
        }

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(connection, sql, map, param as object, transaction, buffered, splitOn);
        }

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(connection, sql, map, param as object, transaction, buffered, splitOn);
        }

        public IEnumerable<dynamic> Query(string sql, dynamic param = null, bool buffered = true)
        {
            return SqlMapper.Query(connection, sql, param as object, transaction, buffered);    
        }

        public Dapper.SqlMapper.GridReader QueryMultiple(string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return SqlMapper.QueryMultiple(connection, sql, param, transaction, commandTimeout, commandType);
        }


        public void Dispose()
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
                connection = null;
            }
        }
    }
}