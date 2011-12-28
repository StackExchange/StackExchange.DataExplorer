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

namespace StackExchange.DataExplorer.Helpers
{

    public static class DapperHelper
    {

        static ConcurrentDictionary<Type, List<string>> paramNameCache = new ConcurrentDictionary<Type, List<string>>();
        static ConcurrentDictionary<Type, string> tableNameMap = new ConcurrentDictionary<Type, string>();

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
        
        public static int? Insert(this DBContext db, string table, dynamic data)
        {
            var o = (object)data;
            List<string> paramNames = GetParamNames(o);

            string cols = string.Join(",", paramNames);
            string cols_params = string.Join(",", paramNames.Select(p => "@" + p));
            var sql = "set nocount on insert " + table + " (" + cols + ") values (" + cols_params + ") select cast(scope_identity() as int)";

            return Query<int?>(db, sql, o).Single();
        }


        public static int Update(this DBContext db, string table, dynamic data)
        {
            List<string> paramNames = GetParamNames(data);

            var builder = new StringBuilder();
            builder.Append("update [").Append(table).Append("] set ");
            builder.AppendLine(string.Join(",", paramNames.Where(n => n != "Id").Select(p => p + "= @" + p)));
            builder.Append("where Id = @Id");

            return Execute(db, builder.ToString(), data);
        }

        public static bool Delete<T>(this DBContext db, int id)
        {
            var name = DetermineTableName<T>(db);
            return db.Execute("delete " + name + " where Id = @id", new { id }) > 0;
        }

        private static string DetermineTableName<T>(DBContext db)
        {
            string name;

            if (!tableNameMap.TryGetValue(typeof(T), out name))
            {
                name = typeof(T).Name;
                if (!TableExists(db, name))
                {
                    // the most stupid pluralizer ever
                    if (TableExists(db, name + "s"))
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

        private static bool TableExists(DBContext db, string name)
        {
            return db.Query("select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @name", new { name }).Count() == 1;
        }

        public static int Execute(this DBContext db, string sql, dynamic param = null, IDbTransaction transaction = null)
        {
            return SqlMapper.Execute(db.Connection, sql, param as object, transaction ?? db.Transaction, commandTimeout: db.CommandTimeout);
        }

        public static IEnumerable<T> Query<T>(this DBContext db, string sql, dynamic param = null, bool buffered = true, int? commandTimeout = null)
        {
           return SqlMapper.Query<T>(db.Connection, sql, param as object, db.Transaction, buffered, commandTimeout);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<dynamic> Query(this DBContext db, string sql, dynamic param = null, bool buffered = true)
        {
            return SqlMapper.Query(db.Connection, sql, param as object, db.Transaction, buffered);    
        }

        public static Dapper.SqlMapper.GridReader QueryMultiple(this DBContext db, string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return SqlMapper.QueryMultiple(db.Connection, sql, param, transaction, commandTimeout, commandType);
        }
    }
}