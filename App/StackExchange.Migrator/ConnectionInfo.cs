using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Migrator
{
    public class ConnectionInfo : IDisposable
    {
        public ConnectionInfo(string name, string connectionString, int commandTimeout)
        {
            Name = name;
            SqlConnection = new SqlConnection(connectionString);
            CommandTimeout = commandTimeout;
        }

        private static readonly Regex SplitOnQuestionMarkRegex = new Regex(@"{\}", RegexOptions.Multiline);

        private SqlCommand GetCommand() 
        {
            var cmd = SqlConnection.CreateCommand();
            cmd.Transaction = transaction;
            return cmd;
        }


        public int Execute(string sql, params object[] parameters)
        {
            using (var cmd = GetCommand())
            {
                PrepareCommand(sql, parameters, cmd);

                return cmd.ExecuteNonQuery();
            }
        }

        private void PrepareCommand(string sql, object[] parameters, SqlCommand cmd)
        {
            cmd.CommandTimeout = CommandTimeout;
            int i = 0;
            foreach (var param in parameters)
            {
                var name = "@param" + i;
                sql = sql.Replace("{" + i + "}", name);
                SqlParameter sqlParam = new SqlParameter(name, param);
                cmd.Parameters.Add(sqlParam);
                i++;
            }
            cmd.CommandText = sql;
            return;
        }

        public bool Exists(string sql, params object[] parameters)
        {
            using (var cmd = GetCommand())
            {
                PrepareCommand(sql, parameters, cmd);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read();
                }
            }
        }

        public SqlConnection SqlConnection { get; private set; }
        public int CommandTimeout { get; private set; }
        public string Name { get; private set; }

        private SqlTransaction transaction;

        public void Dispose()
        {
            if (SqlConnection != null)
            {
                SqlConnection.Dispose();
            }
        }

        internal void BeginTran()
        {
            transaction = SqlConnection.BeginTransaction();
        }

        internal void CommitTran() 
        {
            transaction.Commit();
            transaction = null;
        }

        internal void RollbackTran()
        {
            transaction.Rollback();
            transaction = null;
        }
    }
    
}
