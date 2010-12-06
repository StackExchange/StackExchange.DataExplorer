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
    public class Migration
    {
        private static readonly Regex SplitOnGoRegex = new Regex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private static readonly MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider();

        public static Guid GetMD5(string str)
        {
            lock (md5Provider)
            {
                return new Guid(md5Provider.ComputeHash(Encoding.Unicode.GetBytes(str)));
            }
        }

        public Migration(string path)
        {
            var sql = File.ReadAllText(path);
            SqlCommands = SplitOnGoRegex.Split(sql).Where(s => s.Trim().Length > 0).ToArray();
            Hash = GetMD5(sql).ToString();
            Filename = Path.GetFileName(path);
        }

        public IEnumerable<string> SqlCommands { get; private set; }
        public string Hash { get; private set; }
        public string Filename { get; private set; }

        public void Migrate(ConnectionInfo connection, bool force)
        {
            // ensure migration has not already run
            if (connection.Exists("select 1 from Migrations where [Hash] = {0}", Hash))
            {
                Console.WriteLine("Skipping Migration " + Filename);
                if (!connection.Exists("select 1 from Migrations where [Hash] = {0} and Filename = {1}", Hash, Filename))
                {
                    Console.WriteLine("Filename has changed in the database; updating");
                    connection.Execute("update Migrations set Filename = {0} where [Hash] = {1}", Filename, Hash);
                }
                return;
            }

            Console.WriteLine("Running Migration " + Filename);

            bool exists = false;

            if (connection.Exists("select 1 from Migrations where [Filename] = {0}", Filename))
            {
                if (!force)
                {
                    throw new ApplicationException("Failed to migrate: " + Filename + " - the file was already migrated in the past, to force migration use the --force command");
                }

                exists = true;
            }

            var watch = new System.Diagnostics.Stopwatch();
            try
            {
                watch.Start();
                connection.BeginTran();

                foreach (var sqlCommand in SqlCommands)
                {
                    connection.Execute(sqlCommand);
                }

                watch.Stop();

                if (exists)
                {
                    connection.Execute("UPDATE Migrations SET [Hash] = {0}, [ExecutionDate] = {1}, [Duration] = {2} WHERE [Filename] = {3}",
                        Hash,
                        DateTime.UtcNow,
                        watch.ElapsedMilliseconds,
                        Filename
                        );
                }
                else
                {
                    connection.Execute("INSERT Migrations([Filename], [Hash], [ExecutionDate], [Duration]) VALUES({0}, {1}, {2}, {3})",
                        Filename,
                        Hash,
                        DateTime.UtcNow,
                        watch.ElapsedMilliseconds
                        );
                }

                connection.CommitTran();
            }
            catch (Exception e)
            {
                connection.RollbackTran();
                throw new ApplicationException("Failed to run migration: " + Filename + " " + e.Message);
            }
        }
    }
}
