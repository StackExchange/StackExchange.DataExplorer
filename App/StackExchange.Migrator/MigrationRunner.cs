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
    
    public class MigrationRunner
    {
        IEnumerable<Migration> migrations;
        ConnectionInfo connection;
        bool force;

        public MigrationRunner(ConnectionInfo connection, Config config)
        {
            this.connection = connection;
            this.force = config.Force;
            var migrationPath = config.MigrationPath.IsNullOrEmptyReturn(Environment.CurrentDirectory);
            this.migrations = GetPaths(migrationPath).Select(p => new Migration(p)).ToArray();
        }

        private static IEnumerable<string> GetPaths(string path)
        {
            var files = Directory.GetFiles(path, "*.sql");
            return files;
        }

        public void Migrate()
        {
            Bootstrap();
            
            foreach (var migration in migrations)
            {
                migration.Migrate(connection, force);
            }
            
            return;
        }

        private string LoadResource(string resource)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        private void Bootstrap()
        {
            var sql = LoadResource("Migrator.Bootstrap.sql");
            
            connection.Execute(sql);
            
            return;
        }



        internal static List<ConnectionInfo> GetConnections(Config config)
        {
            var connections = new List<ConnectionInfo>();

            connections.Add
            (
                new ConnectionInfo("DB", config.ConnectionString, config.CommandTimeout)
            );

            return connections;
        }
    }
}
