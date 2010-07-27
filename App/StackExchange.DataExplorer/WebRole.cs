using System.Linq;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;

namespace StackExchange.DataExplorer
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {

            //Get the configuration object
            DiagnosticMonitorConfiguration diagObj = DiagnosticMonitor.GetDefaultInitialConfiguration();

            //Set the service to transfer logs every 15 mins to the storage account
            diagObj.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(15);

            DiagnosticMonitor.Start("DiagnosticsConnectionString", diagObj);

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentChanging;

            return base.OnStart();
        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance
                e.Cancel = true;
            }
        }
    }
}