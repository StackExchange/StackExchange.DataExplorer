using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NDesk.Options;

namespace Migrator
{
    public class Config
    {
        const int DEFAULT_COMMAND_TIMEOUT = 10 * 60;

        public bool Success { get; private set; }
        public bool ShowHelp { get; private set; }

        public string ConnectionString { get;  set; }
        public IEnumerable<string> HostsToMigrate { get; private set; }
        public int CommandTimeout { get; private set; }
        public bool Force { get; private set; }
        public string MigrationPath { get; set; }

        private OptionSet _o;

        public Config(string[] args)
        {
            _o = new OptionSet // http://www.ndesk.org/doc/ndesk-options/NDesk.Options/OptionSet.html
            {
                { "connection=", "REQUIRED - the connection string", v => ConnectionString = v },
                { "migrationPath=", "path to where .sql migration files to be run are located", v => MigrationPath = v },
                { "force", "forces migration of all sql files that were previously migrated", v => Force = v.HasValue() },
                { "timeout=", "number of {seconds} that db commands will execute; default is " + DEFAULT_COMMAND_TIMEOUT, (int v) => CommandTimeout = v },
                { "help", "show this message and exit", v => ShowHelp = v.HasValue() }
            };

            CommandTimeout = DEFAULT_COMMAND_TIMEOUT;

            try
            {
                _o.Parse(args);
            }
            catch (OptionException ex)
            {
                Console.Write("migrator: ");
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                ShowHelp = true;
                return;
            }

            Success = ConnectionString.HasValue();
        }

        public void ShowHelpMessage()
        {
            Console.WriteLine("Usage: migrator [OPTIONS]+");
            Console.WriteLine("  Runs all the *.sql files in the current directory against all");
            Console.WriteLine("  dbs in --tier, which are found in the Sites--db.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            _o.WriteOptionDescriptions(Console.Out);
        }

        public override string ToString()
        {
            return string.Format(
@"Parsed options:
    connection={0}
    force={1}
    timeout={2}
    help={3}", ConnectionString, Force, CommandTimeout, ShowHelp);
        }

    }
}
