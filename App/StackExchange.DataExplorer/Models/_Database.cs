using System;
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
        private DbConnection _connection;
        private int _commandTimeout;
        private DbTransaction _transaction;

        public class Table<T>
        {
            private readonly Database<TDatabase> _database;
            private string _tableName;
            private readonly string _likelyTableName;

            public Table(Database<TDatabase> database, string likelyTableName)
            {
                _database = database;
                _likelyTableName = likelyTableName;
            }

            public string TableName 
            { 
                get 
                {
                    _tableName = _tableName ?? _database.DetermineTableName<T>(_likelyTableName);
                    return _tableName;
                } 
            }

            public int? Insert(dynamic data)
            {
                var o = (object)data;
                var paramNames = GetParamNames(o);

                string cols = string.Join(",", paramNames);
                string cols_params = string.Join(",", paramNames.Select(p => "@" + p));
                var sql = "set nocount on insert " + TableName + " (" + cols + ") values (" + cols_params + ") select cast(scope_identity() as int)";

                return _database.Query<int?>(sql, o).Single();
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

                return _database.Execute(builder.ToString(), parameters);
            }

            public bool Delete(int id) => _database.Execute("delete " + TableName + " where Id = @id", new { id }) > 0;

            public T Get(int id) => _database.Query<T>("select * from " + TableName + " where id = @id", new { id }).FirstOrDefault();

            public T First() => _database.Query<T>("select top 1 * from " + TableName).FirstOrDefault();

            public IEnumerable<T> All() => _database.Query<T>("select * from " + TableName);

            private static readonly ConcurrentDictionary<Type, List<string>> _paramNameCache = new ConcurrentDictionary<Type, List<string>>();
            private static List<string> GetParamNames(object o)
            {
                if (o is DynamicParameters)
                {
                    return (o as DynamicParameters).ParameterNames.ToList();
                }

                List<string> paramNames;
                if (!_paramNameCache.TryGetValue(o.GetType(), out paramNames))
                {
                    paramNames = new List<string>();
                    foreach (var prop in o.GetType().GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public))
                    {
                        paramNames.Add(prop.Name);
                    }
                    _paramNameCache[o.GetType()] = paramNames;
                }
                return paramNames;
            }
        }
        
        public static TDatabase Create(DbConnection connection, int commandTimeout)
        {
            TDatabase db = new TDatabase();
            db.InitDatabase(connection, commandTimeout);
            return db;
        }

        private static Action<Database<TDatabase>> tableConstructor; 

        private void InitDatabase(DbConnection connection, int commandTimeout)
        {
            _connection = connection;
            _commandTimeout = commandTimeout;
            if (tableConstructor == null)
            {
                tableConstructor = CreateTableConstructor(); 
            }
            
            tableConstructor(this);
        }

        public void BeginTransaction(IsolationLevel isolation = IsolationLevel.ReadCommitted)
        {
            _transaction = _connection.BeginTransaction(isolation);
        }

        public void CommitTransaction()
        {
            _transaction.Commit();
            _transaction = null;
        }

        public void RollbackTransaction()
        {
            _transaction.Rollback();
            _transaction = null;
        }

        public Action<Database<TDatabase>> CreateTableConstructor()
        {
            var dm = new DynamicMethod("ConstructInstances", null, new[] { typeof(Database<TDatabase>) }, true);
            var il = dm.GetILGenerator();

            var setters = GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Table<>))
                .Select(p => Tuple.Create(
                        p.GetSetMethod(true),
                        p.PropertyType.GetConstructor(new[] { typeof(Database<TDatabase>), typeof(string) }),
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

        private static readonly ConcurrentDictionary<Type, string> _tableNameMap = new ConcurrentDictionary<Type, string>();
        private string DetermineTableName<T>(string likelyTableName)
        {
            string name;

            if (!_tableNameMap.TryGetValue(typeof(T), out name))
            {
                name = likelyTableName;
                if (!TableExists(name))
                {
                    name = typeof(T).Name;
                }

                _tableNameMap[typeof(T)] = name;
            }
            return name;
        }

        private bool TableExists(string name)
        {
            return _connection.Query("select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = @name", new { name }, transaction: _transaction).Count() == 1;
        }

        public int Execute(string sql, dynamic param = null) => 
            _connection.Execute(sql, param as object, _transaction, commandTimeout: _commandTimeout);

        public IEnumerable<T> Query<T>(string sql, dynamic param = null, bool buffered = true) => 
            _connection.Query<T>(sql, param as object, _transaction, buffered, _commandTimeout);

        public IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null) => 
            _connection.Query(sql, map, param as object, transaction, buffered, splitOn);

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null) => 
            _connection.Query(sql, map, param as object, transaction, buffered, splitOn);

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null) => 
            _connection.Query(sql, map, param as object, transaction, buffered, splitOn);

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null) => 
            _connection.Query(sql, map, param as object, transaction, buffered, splitOn);

        public IEnumerable<dynamic> Query(string sql, dynamic param = null, bool buffered = true) => 
            _connection.Query(sql, param as object, _transaction, buffered);

        public SqlMapper.GridReader QueryMultiple(string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null) => 
            SqlMapper.QueryMultiple(_connection, sql, param, transaction, commandTimeout, commandType);


        public void Dispose()
        {
            if (_connection.State != ConnectionState.Closed)
            {
                _transaction?.Rollback();
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}