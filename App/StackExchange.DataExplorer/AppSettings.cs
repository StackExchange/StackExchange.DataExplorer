using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using StackExchange.DataExplorer.Helpers;
using Newtonsoft.Json;

namespace StackExchange.DataExplorer
{
    public class AppSettings
    {
        static AppSettings()
        {
            Refresh(); 
        }

        [Default(120)]
        public static int QueryTimeout { get; private set; }

        [Default(2)]
        public static int ConcurrentQueries { get; private set; }

        [Default(50000)]
        public static int MaxResultsPerResultSet { get; private set; }

        [Default(100000)]
        public static int MaxTotalResults { get; private set; }

        [Default(true)]
        public static bool FetchDataInReadUncommitted { get; private set; }

        // guess the user id based on an email hash (only used on StackExchange dbs)
        [Default(true)]
        public static bool GuessUserId { get; private set; }

        [Default(false)]
        public static bool EnableEnforceSecureOpenId { get; private set; }

        [Default(false)]
        public static bool EnforceSecureOpenIdDefault { get; private set; }

        [Default(false)]
        public static bool EnableWhiteList { get; private set; }

        [Default(false)]
        public static bool AllowExcludeMetaOption { get; private set; }

        [Default(false)]
        public static bool AllowRunOnAllDbsOption { get; private set; }

        [Default(true)]
        public static bool EnableCancelQuery { get; private set; }

        [Default(false)]
        public static bool EnableBypassCache { get; private set; }

        [Default("")]
        public static string RecaptchaPublicKey { get; private set; }

        [Default("")]
        public static string RecaptchaPrivateKey { get; private set; }

        [Default(-1)]
        public static int AutoExpireCacheMinutes { get; private set; }

        [Default(null)]
        public static HelperTableCachePreferences HelperTableOptions { get; private set; }

        [Default(false)]
        public static bool EnableOdata { get; private set; }

        [Default(AuthenitcationMethod.Default)]
        public static AuthenitcationMethod AuthMethod { get; private set; }

        [Default("")]
        public static string ActiveDirectoryViewGroups { get; private set; }

        [Default("")]
        public static string ActiveDirectoryAdminGroups { get; private set; }

        [Default("")]
        public static string OAuthSessionSalt { get; private set; }

        [Default("")]
        public static string GoogleOAuthClientId { get; private set; }

        [Default("")]
        public static string GoogleOAuthSecret { get; private set; }

        public static bool EnableGoogleLogin { get { return GoogleOAuthClientId.HasValue() && GoogleOAuthSecret.HasValue(); } }


        public enum AuthenitcationMethod
        {
            Default, // OpenID & Oauth
            ActiveDirectory
        }

        public static Action Refreshed;

        public static void Refresh()
        {
            var data = Current.DB.AppSettings.All().ToDictionary(v => v.Setting, v => v.Value);

            // Also allow overriding keys from the web.config
            foreach (var k in ConfigurationManager.AppSettings.AllKeys)
            {
                data[k] = ConfigurationManager.AppSettings[k];
            }

            foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                string overrideData;

                if (data.TryGetValue(property.Name, out overrideData))
                {
                    if (property.PropertyType == typeof(bool))
                    {
                        bool parsed;
                        Boolean.TryParse(overrideData, out parsed);
                        property.SetValue(null, parsed, null);
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        int parsed;
                        if (int.TryParse(overrideData, out parsed))
                        {
                            property.SetValue(null, parsed, null);
                        }
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(null, overrideData, null);
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(null, Enum.Parse(property.PropertyType, overrideData), null);
                    }
                    else if (overrideData[0] == '{' && overrideData[overrideData.Length - 1] == '}')
                    {
                        try
                        {
                            property.SetValue(null, JsonConvert.DeserializeObject(overrideData, property.PropertyType), null);
                        }
                        catch (JsonSerializationException)
                        {
                            // Just in case
                            property.SetValue(null, null, null);
                        }
                    }
                }
                else
                {
                    var attribs = property.GetCustomAttributes(typeof (DefaultAttribute), false);
                    if (attribs.Length > 0)
                    {
                        var attrib = (DefaultAttribute) attribs[0];
                        property.SetValue(null, attrib.DefaultValue, null);
                    }
                }
            }
            // For anyone who wants to listen and update their downstream data...
            var handler = Refreshed;
            if (handler != null) handler();
        }
    }
}