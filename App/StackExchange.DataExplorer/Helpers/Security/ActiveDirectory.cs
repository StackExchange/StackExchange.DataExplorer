using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading;
using StackExchange.DataExplorer.Models;
using StackExchange.Profiling;

namespace StackExchange.DataExplorer.Helpers.Security
{
    public class ActiveDirectory
    {
        private static List<string> _adminGroups;
        private static List<string> _userGroups; 
        private static ConcurrentDictionary<string, bool> _admins;
        private static ConcurrentDictionary<string, bool> _users;

        static ActiveDirectory()
        {
            AppSettings.Refreshed += AppSettingsRefresh;
            AppSettingsRefresh();
        }

        private static void AppSettingsRefresh()
        {
            _adminGroups = AppSettings.ActiveDirectoryAdminGroups.Split(StringSplits.Comma_SemiColon).ToList();
            _userGroups = AppSettings.ActiveDirectoryViewGroups.Split(StringSplits.Comma_SemiColon).Concat(_adminGroups).ToList();
            _admins = new ConcurrentDictionary<string, bool>();
            _users = new ConcurrentDictionary<string, bool>();
        }

        public static bool AuthenticateUser(string userName, string password)
        {
            var authed = RunCommand(pc => pc.ValidateCredentials(userName, password));
            if (authed)
            {
                bool dontCare;
                _admins.TryRemove(userName, out dontCare);
                _users.TryRemove(userName, out dontCare);
            }
            return authed;
        }

        public static bool IsAdmin(string userName) => IsMember(userName, _admins, _adminGroups);

        public static bool IsUser(string userName) => IsMember(userName, _users, _userGroups);

        public static void SetProperties(User user)
        {
            RunCommand(pc =>
            {
                using (var up = UserPrincipal.FindByIdentity(pc, user.ADLogin))
                {
                    if (up == null) return "";
                    user.Login = up.DisplayName;
                    user.Email = up.EmailAddress;
                    user.AboutMe = up.Description;

                    Current.DB.Users.Update(user.Id, new {user.Login, user.Email, user.AboutMe});

                    return "";
                }
            });
        }

        private static bool IsMember(string userName, ConcurrentDictionary<string, bool> dict, IReadOnlyCollection<string> groupNames)
        {
            if (groupNames.Count == 0) return false;
            // Allow-all special case
            if (groupNames.Any(n => n == "*")) return true;

            bool value;
            if (dict.TryGetValue(userName, out value)) return value;
            dict[userName] = value = RunCommand(pc =>
            {
                using (MiniProfiler.Current.Step("Getting group access for " + userName))
                {
                    foreach (var group in groupNames)
                    {
                        using (var gp = GroupPrincipal.FindByIdentity(pc, @group))
                        {
                            if (gp == null) continue;
                            // Fastest recursive way to get groups, especially when going against a remote DC
                            if (gp.GetMembers(true)
                                .Select(m => m.SamAccountName)
                                .Any(sam => string.Equals(sam, userName, StringComparison.InvariantCultureIgnoreCase)))
                                return true;
                        }
                    }
                }
                return false;
            });
            return value;
        }

        public static T RunCommand<T>(Func<PrincipalContext, T> command, int retries = 3)
        {
            try
            {
                using (var pc = new PrincipalContext(ContextType.Domain))
                    return command(pc);
            }
            catch (Exception ex)
            {
                if (retries > 0)
                {
                    Thread.Sleep(500);
                    return RunCommand(command, retries - 1);
                }
                else
                {
                    Current.LogException("Could not contact current AD", ex);
                }
            }
            return default(T);
        }
    }
}
