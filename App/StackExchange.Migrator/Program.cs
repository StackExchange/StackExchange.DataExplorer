using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlClient;

namespace Migrator
{
    public class Program
    {
        static int Main(string[] args)
        {
            var config = new Config(args);
            if (!config.Success || config.ShowHelp)
            {
                config.ShowHelpMessage();
                return -1;
            }
            
            List<ConnectionInfo> connections = null;
            try
            {
                connections = MigrationRunner.GetConnections(config);

                foreach (var c in connections)
                {
                    Console.WriteLine("\nConnecting to database: " + c.Name);
                    c.SqlConnection.Open();
                    var runner = new MigrationRunner(c, config);
                    Console.WriteLine("Running migrations");
                    runner.Migrate();
                    c.SqlConnection.Close();
                    Console.WriteLine(c.Name + " is up to date");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\n\nERROR OCCURRED WHEN RUNNING MIGRATIONS:");
                Console.WriteLine(e.Message);
                return -1;
            }
            finally
            {
                if (connections != null)
                {
                    foreach (var connection in connections)
                    {
                        try { connection.Dispose(); }
                        catch { Console.WriteLine("ERROR: Failed to close connection"); }
                    }
                }
            }

            return 0;
        }
        
    }

    static class ProgramExtensions
    {
        /// <summary>
        /// Answers true if this String is either null or empty.
        /// </summary>
        /// <remarks>I'm so tired of typing String.IsNullOrEmpty(s)</remarks>
        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Answers true if this String is neither null or empty.
        /// </summary>
        /// <remarks>I'm also tired of typing !String.IsNullOrEmpty(s)</remarks>
        public static bool HasValue(this string s)
        {
            return !string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Returns the first non-null/non-empty parameter when this String is null/empty.
        /// </summary>
        public static string IsNullOrEmptyReturn(this string s, params string[] otherPossibleResults)
        {
            if (s.HasValue())
                return s;

            for (int i = 0; i < (otherPossibleResults ?? new string[0]).Length; i++)
            {
                if (otherPossibleResults[i].HasValue())
                    return otherPossibleResults[i];
            }

            return "";
        }
    }
}
