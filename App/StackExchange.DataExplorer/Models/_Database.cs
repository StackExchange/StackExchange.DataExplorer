﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Data.Common;
using System.Reflection.Emit;

namespace Dapper
{
    public abstract class Database<TDatabase> : IDisposable where TDatabase : Database<TDatabase>, new()
    {
        public class Table<T>
        {
            Database<TDatabase> database;
            string tableName;
            string likelyTableName;

            public Table(Database<TDatabase> database, string likelyTableName)
            {
                this.database = database;
                this.likelyTableName = likelyTableName;
            }

            public string TableName 
            { 
                get 
                {
                    tableName = tableName ?? database.DetermineTableName<T>(likelyTableName);
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

            public T First()
            {
                return database.Query<T>("select top 1 * from " + TableName).FirstOrDefault();
            }

            public IEnumerable<T> All()
            {
                return database.Query<T>("select * from " + TableName);
            }

            static ConcurrentDictionary<Type, List<string>> paramNameCache = new ConcurrentDictionary<Type, List<string>>();
            private static List<string> GetParamNames(object o)
            {
                if (o is DynamicParameters)
                {
                    return (o as DynamicParameters).ParameterNames.ToList();
                }

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


        public static TDatabase Create(DbConnection connection, int commandTimeout)
        {
            TDatabase db = new TDatabase();
            db.InitDatabase(connection, commandTimeout);
            return db;
        }

        private static Action<Database<TDatabase>> tableConstructor; 

        private void InitDatabase(DbConnection connection, int commandTimeout)
        {
            this.connection = connection;
            this.commandTimeout = commandTimeout;
            if (tableConstructor == null)
            {
                tableConstructor = CreateTableConstructor(); 
            }
            
            tableConstructor(this);
        }

        public void BeginTransaction(IsolationLevel isolation = IsolationLevel.ReadCommitted)
        {
            transaction = connection.BeginTransaction(isolation);
        }

        public void CommitTransaction()
        {
            transaction.Commit();
            transaction = null;
        }

        public void RollbackTransaction()
        {
            transaction.Rollback();
            transaction = null;
        }

        public Action<Database<TDatabase>> CreateTableConstructor()
        {
            var dm = new DynamicMethod("ConstructInstances", null, new Type[] { typeof(Database<TDatabase>) }, true);
            var il = dm.GetILGenerator();

            var setters = GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Table<>))
                .Select(p => Tuple.Create(
                        p.GetSetMethod(true),
                        p.PropertyType.GetConstructor(new Type[] { typeof(Database<TDatabase>), typeof(string) }),
                        p.Name,
                        p.DeclaringType
                 ));

            foreach (var setter in setters)
            {
                il.Emit(OpCodes.Ldarg_0);
                // [db]

                il.Emit(OpCodes.Ldstr, setter.Item3);
                // [db, likelyname]

                il.Emit(OpCodes.Newobj, setter.Item2);
                // [table]

                var table = il.DeclareLocal(setter.Item2.DeclaringType);
                il.Emit(OpCodes.Stloc, table);
                // []

                il.Emit(OpCodes.Ldarg_0);
                // [db]

                il.Emit(OpCodes.Castclass, setter.Item4);
                // [db cast to container]

                il.Emit(OpCodes.Ldloc, table);
                // [db cast to container, table]

                il.Emit(OpCodes.Callvirt, setter.Item1);
                // []
            }
 
            il.Emit(OpCodes.Ret);
            return (Action<Database<TDatabase>>)dm.CreateDelegate(typeof(Action<Database<TDatabase>>));
        }

        static ConcurrentDictionary<Type, string> tableNameMap = new ConcurrentDictionary<Type, string>();
        private string DetermineTableName<T>(string likelyTableName)
        {
            string name;

            if (!tableNameMap.TryGetValue(typeof(T), out name))
            {
                name = likelyTableName;
                if (!TableExists(name))
                {
                    name = typeof(T).Name;
                }

                tableNameMap[typeof(T)] = name;
            }
            return name;
        }

        private bool TableExists(string name)
        {
            return connection.Query("select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @name", new { name }, transaction: transaction).Count() == 1;
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
                if (transaction != null)
                {
                    transaction.Rollback();
                }

                connection.Close();
                connection = null;
            }
        }
    }
}