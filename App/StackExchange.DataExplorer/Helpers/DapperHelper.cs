using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;
using System.Data;
using Dapper;

namespace StackExchange.DataExplorer.Helpers
{

    public static class DapperHelper
    {

        class ConnectionDisposer : IDisposable
        {
            IDbConnection cnn; 

            public ConnectionDisposer(IDbConnection cnn)
            {
                this.cnn = cnn;
                if (cnn.State != ConnectionState.Open)
                {
                    cnn.Open();
                }
            }

            public void Dispose()
            {
                if (cnn.State == ConnectionState.Open)
                {
                    cnn.Close();
                }
            }
        }

       
        public static int Execute(this DBContext db, string sql, dynamic param = null, IDbTransaction transaction = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Execute(db.Connection, sql, param as object, transaction ?? db.Transaction, commandTimeout: db.CommandTimeout);
        }


        public static IEnumerable<T> Query<T>(this DBContext db, string sql, dynamic param = null, bool buffered = true, int? commandTimeout = null)
        {
           using (new ConnectionDisposer(db.Connection))
           return SqlMapper.Query<T>(db.Connection, sql, param as object, db.Transaction, buffered, commandTimeout);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this DBContext db, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, dynamic param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Query(db.Connection, sql, map, param as object, db.Transaction, buffered, splitOn);
        }

        public static IEnumerable<dynamic> Query(this DBContext db, string sql, dynamic param = null, bool buffered = true)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.Query(db.Connection, sql, param as object, db.Transaction, buffered);    
        }

        public static Dapper.SqlMapper.GridReader QueryMultiple(this DBContext db, string sql, dynamic param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            using (new ConnectionDisposer(db.Connection))
            return SqlMapper.QueryMultiple(db.Connection, sql, param, transaction, commandTimeout, commandType);
        }
    }
}