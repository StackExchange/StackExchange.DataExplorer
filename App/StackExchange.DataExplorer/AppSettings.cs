using System;
using System.Linq;
using System.Reflection;
using Dapper;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

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

        // guess the user id based on an email hash (only used on StackExchange dbs)
        [Default(true)]
        public static bool GuessUserId { get; private set; }

        [Default(false)]
        public static bool EnableWhiteList { get; private set; }

        [Default(false)]
        public static bool AllowExcludeMetaOption { get; private set; }

        [Default(false)]
        public static bool AllowRunOnAllDbsOption { get; private set; }

        [Default(false)]
        public static bool EnableEditorSuggestions { get; private set; }

        [Default(false)]
        public static bool EnableAdvancedSqlErrors { get; private set; }

        [Default("")]
        public static string RecaptchaPublicKey { get; private set; }

        [Default("")]
        public static string RecaptchaPrivateKey { get; private set; }

        [Default(-1)]
        public static int AutoExpireCacheMinutes { get; private set; }

        public static void Refresh()
        {
            var data = Current.DB.AppSettings.All().ToDictionary(v => v.Setting, v => v.Value);

            foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                string overrideData;
                if (data.TryGetValue(property.Name, out overrideData))
                {
                    if (property.PropertyType == typeof(bool))
                    {
                        bool parsed = false;
                        Boolean.TryParse(overrideData, out parsed);
                        property.SetValue(null, parsed, null);
                    }

                    if (property.PropertyType == typeof(int))
                    {
                        int parsed = -1;
                        if (int.TryParse(overrideData, out parsed))
                        {
                            property.SetValue(null, parsed, null);
                        }
                    }

                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(null, overrideData, null);
                    }

                }
                else
                {
                    DefaultAttribute attrib = (DefaultAttribute)property.GetCustomAttributes(typeof(DefaultAttribute), false)[0];
                    property.SetValue(null, attrib.DefaultValue, null);
                }
            }
        }
    }
}