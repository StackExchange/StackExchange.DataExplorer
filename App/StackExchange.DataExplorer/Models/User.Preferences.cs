using System;
using System.Collections.Generic;
using StackExchange.DataExplorer.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Models
{
    
    partial class User
    {
        private UserPreferenceDictionary _preferences;

        public bool EnforceSecureOpenId
        {
            get
            {
                return Preferences.Get(Preference.EnforceSecureOpenId, AppSettings.EnforceSecureOpenIdDefault);
            }
            set
            {
                Preferences.Set(Preference.EnforceSecureOpenId, value);
                SavePreferences();
            }
        }

        public bool HideSchema
        { 
            get 
            {
                return Preferences.Get(Preference.HideSchema, false);
            } 
            set 
            {
                if (value != HideSchema)
                {
                    Preferences.Set(Preference.HideSchema, value);
                    SavePreferences();
                }
            } 
        }

        public string DefaultQuerySort
        {
            get
            {
                return Preferences.Get<string>(Preference.DefaultQuerySort, null);
            }
            set
            {
                if (value != DefaultQuerySort)
                {
                    Preferences.Set(Preference.DefaultQuerySort, value);
                    SavePreferences();
                }
            }
        }

        public int? DefaultQueryPageSize
        {
            get
            {
                var value = Preferences.Get(Preference.DefaultQueryPageSize, -1);
                return value == -1 ? (int?)null : value;
            }
            set
            {
                if (value != DefaultQueryPageSize)
                {
                    Preferences.Set(Preference.DefaultQueryPageSize, value ?? -1);
                    SavePreferences();
                }
            }
        }

        /// <summary>
        /// Contains key/value data for this User - will be null until InitPreferences is called.
        /// </summary>
        public UserPreferenceDictionary Preferences
        {
            get
            {
                if (!PreferencesInitialized)
                    InitPreferences();

                return _preferences;
            }
        }

        public bool PreferencesInitialized => _preferences != null;

        private void InitPreferences()
        {
            string serializedPrefs = null;

            if (IsAnonymous) // anon can still have some prefs stored in the cookie
            {
                /* skip for now */

                //var controller = Current.Controller;
                //if (controller != null && controller.Cookie != null)
                //    serializedPrefs = controller.Cookie.Preferences;
            }
            else
            {
                // pref raw is now no longer defer loaded, which means we don't go to dapper for it
                serializedPrefs = this.PreferencesRaw;
            }

            _preferences = new UserPreferenceDictionary(serializedPrefs);
        }

        /// <summary>
        /// </summary>
        public void SavePreferences()
        {
            if (!PreferencesInitialized || !Preferences.HasChanges)
                return;

            string prefs = Preferences.Serialize();

            if (IsAnonymous)
            {
                // skip for now 
                // controller base will save cookies for anon users, just set the value
                //  Current.Controller.Cookie.Preferences = prefs;
            }
            else
            {
                PreferencesRaw = prefs;
                Current.DB.Execute("update Users set PreferencesRaw = @prefs where Id = @Id", new { prefs, Id });
            }
        }

    }
}

namespace StackExchange.DataExplorer.Helpers
{
    /// <summary>
    /// Keys for any user preferences.
    /// </summary>
    public enum Preference
    {
        HideSchema = 1,
        DefaultQuerySort = 2,
        DefaultQueryPageSize = 3,
        EnforceSecureOpenId = 4
    }

    public static class PreferenceKey
    {
        public const string ProflileSummaryExpand = "ps-e";
        public const string ProflileSummaryExpandSelf = "ps-es";
    }

    public class UserPreferenceDictionary
    {
        public bool HasChanges { get; private set; }
        private readonly Dictionary<string, string> _prefs;

        public UserPreferenceDictionary(string serializedState)
        {
            _prefs = string.IsNullOrEmpty(serializedState) ?
                new Dictionary<string, string>() :
                Deserialize(serializedState);
        }

        public bool HasValue(Preference key)
        {
            string k = ((int)key).ToString();
            return _prefs.ContainsKey(k) && _prefs[k] != null;
        }

        // to avoid excessive internal int -> string conversions
        private bool HasValuePrivate(string k) => _prefs.ContainsKey(k) && _prefs[k] != null;
        public T Get<T>(Preference key) => Get(key, default(T));
        public T Get<T>(Preference key, T defaultValue) => Get(((int)key).ToString(), defaultValue);

        public T Get<T>(string key, T defaultValue)
        {
            T result = defaultValue;
            var defaultValueType = typeof(T);

            string v = HasValuePrivate(key) ? _prefs[key] : "";

            if (!v.IsNullOrEmpty())
            {
                if (defaultValueType.IsEnum)
                    result = (T)Enum.Parse(defaultValueType, v);
                else
                    result = (T)Convert.ChangeType(v, defaultValueType);
            }

            return result;
        }

        public void Set(Preference key, object value)
        {
            Set(((int)key).ToString(), value, toIntEnums: true);
        }

        public void Set(string key, object value, bool toIntEnums = false)
        {
            if (value == null)
            {
                if (HasValuePrivate(key))
                {
                    HasChanges = true;
                    _prefs.Remove(key);
                }
            }
            else
            {
                string s = value is Enum && toIntEnums ? ((int)value).ToString() : value.ToString();
                if (s.Contains("]"))
                    throw new InvalidOperationException("Preference values cannot contain unescaped ']' characters");
                string old;
                if (!_prefs.TryGetValue(key, out old) || old != s)
                {   // new or different
                    HasChanges = true;
                    _prefs[key] = s;
                }
            }
        }

        public string Serialize()
        {
            if (_prefs.Count == 0) return "";
            var sb = new StringBuilder(_prefs.Count * 7);
            foreach (var p in _prefs)
            {
                if (p.Value != null)
                {
                    sb.Append("[");
                    sb.Append(p.Key);
                    sb.Append("|");
                    sb.Append(p.Value);
                    sb.Append("]");
                }
            }
            return sb.ToString();
        }

        private static readonly Regex _prefPattern = new Regex(@"\[(?<key>[^|]*)\|(?<value>[^\]]*)\]", RegexOptions.Compiled);

        private Dictionary<string, string> Deserialize(string serializedState)
        {
            var result = new Dictionary<string, string>();
            foreach (Match m in _prefPattern.Matches(serializedState))
            {
                result.Add(m.Groups["key"].Value, m.Groups["value"].Value);
            }
            return result;
        }
    }
}