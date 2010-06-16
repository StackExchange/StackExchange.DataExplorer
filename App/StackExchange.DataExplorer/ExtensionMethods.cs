using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Specialized;
using System.Data.Common;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.Controllers;
using System.Web.Routing;
using System.Text.RegularExpressions;
using StackExchange.DataExplorer.Helpers;
using System.IO.Compression;
using System.Data.SqlClient;
using System.Data;
using System.Web.Mvc;

namespace StackExchange.DataExplorer {
    public static class ExtensionMethods {

        /// <summary>
        /// returns a hash of the time that is stable for up to 16 minutes (999 seconds)
        /// </summary>
        public static string GetHashedTime() {
            // one tick is 10ns; ticks are 18 characters long
            return ("ticks=" + DateTime.UtcNow.Ticks.ToString().Substring(0, 8)).ToCrc16();
        }

        /// <summary>
        /// Answers true if this String is either null or empty.
        /// </summary>
        /// <remarks>I'm so tired of typing String.IsNullOrEmpty(s)</remarks>
        public static bool IsNullOrEmpty(this string s) {
            return string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Answers true if this String is neither null or empty.
        /// </summary>
        /// <remarks>I'm also tired of typing !String.IsNullOrEmpty(s)</remarks>
        public static bool HasValue(this string s) {
            return !string.IsNullOrEmpty(s);
        }

        /// <summary>
        /// Returns the first non-null/non-empty parameter when this String is null/empty.
        /// </summary>
        public static string IsNullOrEmptyReturn(this string s, params string[] otherPossibleResults) {
            if (s.HasValue())
                return s;

            for (int i = 0; i < (otherPossibleResults ?? new string[0]).Length; i++) {
                if (otherPossibleResults[i].HasValue())
                    return otherPossibleResults[i];
            }

            return "";
        }

        /// <summary>
        /// returns string with any mulitple, sequential spaces replaced by a single space, 
        /// and any extra spaces trimmed from beginning and end.
        /// </summary>
        public static string RemoveExtraSpaces(this string s) {
            if (s.IsNullOrEmpty()) return s;
            s = Regex.Replace(s, "\u200c", " "); // see http://en.wikipedia.org/wiki/Zero-width_non-joiner
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s;
        }

        /// <summary>
        /// force string to be maxlen or smaller
        /// </summary>
        public static string Truncate(this string s, int maxLength) {
            if (s.IsNullOrEmpty()) return s;
            return (s.Length > maxLength) ? s.Remove(maxLength) : s;
        }

        public static string TruncateWithEllipsis(this string s, int maxLength) {
            if (s.IsNullOrEmpty()) return s;
            if (s.Length <= maxLength) return s;

            return string.Format("{0}...", Truncate(s, maxLength - 3));
        }

        /// <summary>
        /// Produces a URL-friendly version of this String, "like-this-one".
        /// </summary>
        public static string URLFriendly(this string s) {
            return s.HasValue() ? HtmlUtilities.URLFriendly(s) : s;
        }

        /// <summary>
        /// returns Url Encoded string
        /// </summary>
        public static string UrlEncode(this string s) {
            return s.HasValue() ? HttpUtility.UrlEncode(s) : s;
        }

        /// <summary>
        /// returns Html Encoded string
        /// </summary>
        public static string HtmlEncode(this string s) {
            return s.HasValue() ? HttpUtility.HtmlEncode(s) : s;
        }

        /// <summary>
        /// returns true if this looks like a semi-valid email address
        /// </summary>
        public static bool IsEmailAddress(this string s) {
            return s.HasValue() ? Regex.IsMatch(s, @"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,4}$", RegexOptions.IgnoreCase) : false;
        }

        /// <summary>
        /// returns true if this looks like a semi-valid openid string; it starts with "=@+$!(" or contains a period.
        /// </summary>
        public static bool IsOpenId(this string s) {
            return s.HasValue() ? Regex.IsMatch(s, @"^[=@+$!(]|.*?\.") : false;
        }

        #region NameValueCollection (aka Request.Form) Get Helpers

        public static List<T> GetList<T>(this NameValueCollection nvc, string key, char delimiter) {
            string[] values = Get<string>(nvc, key).Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            List<T> result = new List<T>(values.Length);
            Type tType = typeof(T);

            if (tType == typeof(string)) {
                result.AddRange((IEnumerable<T>)(object)values);
            } else {
                foreach (string s in values) {
                    try {
                        result.Add((T)Convert.ChangeType(s.Trim(), tType));
                    } catch (Exception ex) {
                        throw new FormatException(string.Format("Unable to convert \"{0}\" into a {1}.", s, tType.FullName), ex);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Answers true if a value is found for 'key'
        /// </summary>
        public static bool Contains(this NameValueCollection nvc, string key) {
            return !string.IsNullOrEmpty(nvc[key]);
        }

        public static T Get<T>(this NameValueCollection nvc, string key) {
            return Get(nvc, key, default(T), false);
        }

        public static T Get<T>(this NameValueCollection nvc, string key, T defaultValue) {
            return Get(nvc, key, defaultValue, false);
        }

        private static T Get<T>(NameValueCollection nvc, string key, T defaultValue, bool throwExceptionWhenValueIsEmpty) {
            T result = defaultValue;
            Type resultType = typeof(T);

            string value = nvc[key];

            if (string.IsNullOrEmpty(value)) {
                if (throwExceptionWhenValueIsEmpty) {
                    throw new HttpRequestValidationException(string.Format("Unable to find a Form value for key '{0}'.", key));
                }
            } else {
                try {
                    // Some controls submit "on" when checked or selected..
                    if (resultType == typeof(bool) && value.Equals("on", StringComparison.OrdinalIgnoreCase)) {
                        value = "True";
                    }

                    if (resultType == typeof(string)) {
                        // don't trim strings here; this breaks markdown parsing, as leading spaces can be significant
                        result = (T)Convert.ChangeType(HtmlUtilities.RemoveInvalidXMLChars(value), resultType);
                    } else if (resultType.IsEnum) {
                        result = (T)Enum.Parse(resultType, value);
                    } else {
                        result = (T)Convert.ChangeType(value.Trim(), resultType);
                    }
                } catch (Exception ex) {
                    throw new HttpRequestValidationException(string.Format(
                        "Encountered a problem trying to convert '{0}' to a {1}.", value ?? "NULL", resultType.FullName), ex);
                }
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Returns a set of name/value dictionary pairs for an arbitary SQL query
        /// </summary>
        public static IEnumerable<Dictionary<string, object>> ExecuteQuery(this Models.DBContext ctx, string query) {
            return ExecuteQuery(ctx, query, null);
        }

        /// <summary>
        /// Returns a set of name/value dictionary pairs for an arbitary parameterized SQL query
        /// </summary>
        public static IEnumerable<Dictionary<string, object>> ExecuteQuery(this Models.DBContext db, string query, object parameterHash) {
            using (var cmd = db.Connection.CreateCommand()) {
                if (parameterHash != null)
                    SetupParameterizedCommand(cmd, query, parameterHash);
                else
                    cmd.CommandText = query;

                db.Connection.Open();
                using (DbDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection)) {
                    while (rdr.Read()) {
                        var d = new Dictionary<string, object>();
                        for (int i = 0; i < rdr.FieldCount; i++) {
                            d.Add(rdr.GetName(i), rdr.GetValue(i));
                        }
                        yield return d;
                    }
                }
            }
        }

        private static void SetupParameterizedCommand(DbCommand cmd, string query, object parameterHash) {
            var props = parameterHash.GetType().GetProperties();
            var parameterNames = new object[props.Length];

            for (int i = 0; i < props.Length; i++) {
                var prop = props[i];
                var name = "@" + prop.Name;
                var value = prop.GetValue(parameterHash, null);

                parameterNames[i] = name;
                // When we need to pass exlicit db data types, we'll probably know it in dev via exception..
                cmd.Parameters.Add(new SqlParameter { ParameterName = name, Value = value });
            }

            // All our format placeholders will be nicely replaced with the parameter names..
            cmd.CommandText = string.Format(query, parameterNames);
        }


        public static IEnumerable<object[]> ExecuteQueryArray(this Models.DBContext ctx, string query) {
            return ExecuteQueryArray(ctx, query, null);
        }

        /// <summary>
        /// Returns a set of object arrays for an arbitary SQL query (intended for very simple queries only!)
        /// </summary>
        /// <remarks>http://geeks.ms/blogs/ohernandez/archive/2008/01/30/executing-arbitrary-queries-in-linq-to-sql.aspx</remarks>
        public static IEnumerable<object[]> ExecuteQueryArray(this Models.DBContext ctx, string query, object parameterHash) {
            using (DbCommand cmd = ctx.Connection.CreateCommand()) {

                if (parameterHash != null)
                    SetupParameterizedCommand(cmd, query, parameterHash);
                else
                    cmd.CommandText = query;

                cmd.CommandText = query;
                ctx.Connection.Open();
                using (DbDataReader rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection)) {
                    while (rdr.Read()) {
                        object[] res = new object[rdr.FieldCount];
                        rdr.GetValues(res);
                        yield return res;
                    }
                }
            }
        }

        /// <summary>
        /// returns the DBContext used by this ViewUserControl's Controller.
        /// </summary>
        public static DBContext DB(this System.Web.Mvc.ViewUserControl vuc) {
            StackOverflowController c = vuc.ViewContext.Controller as StackOverflowController;
            if (c == null) throw new ArgumentException("Unable to find a ControllerBase on ViewUserControl " + vuc.GetType().FullName);
            return c.DB;
        }

        /// <summary>
        /// returns the DBContext used by this ViewPage's Controller.
        /// </summary>
        public static DBContext DB(this System.Web.Mvc.ViewPage vp) {
            StackOverflowController c = vp.ViewContext.Controller as StackOverflowController;
            if (c == null) throw new ArgumentException("Unable to find a ControllerBase on ViewPage " + vp.GetType().FullName);
            return c.DB;
        }


        /// <summary>
        /// Adds the url to the RouteCollection with the specified defaults.
        /// </summary>
        /// <remarks>Added to remove annoying empty space in all MapRoute calls.</remarks>
        public static void MapRoute(this RouteCollection routes, string url, object defaults) {
            routes.MapRoute("", url, defaults);
        }

        public static string Temperature(this DateTime dt) {
            TimeSpan ts = DateTime.UtcNow - dt;
            double delta = ts.TotalMinutes;

            if (delta < 10) {
                return "supernova";
            } else if (delta < 120) {
                return "warm";
            }

            return "cool";
        }

        public static string Temperature(this DateTime? dt) {
            if (dt == null) {
                return "cool";
            }
            return Temperature(dt.Value);
        }

        /// <summary>
        /// Returns a unix Epoch time given a Date
        /// </summary>
        public static long ToEpochTime(this DateTime dt) {
            return (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        /// <summary>
        /// Converts to Date given an Epoch time
        /// </summary>
        public static DateTime ToDateTime(this long epoch) {
            return new DateTime(1970, 1, 1, 0, 0, 0).AddSeconds(epoch);
        }

        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// </summary>
        public static string ToRelativeTime(this DateTime dt) {
            DateTime utcNow = DateTime.UtcNow;
            return dt <= utcNow ? ToRelativeTimePast(dt, utcNow) : ToRelativeTimeFuture(dt, utcNow);
        }
        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// If this DateTime is null, returns empty string.
        /// </summary>
        public static string ToRelativeTime(this DateTime? dt) {
            if (dt == null) return "";
            return ToRelativeTime(dt.Value);
        }

        private static string ToRelativeTimePast(DateTime dt, DateTime utcNow) {
            TimeSpan ts = utcNow - dt;
            double delta = ts.TotalSeconds;

            if (delta < 60) {
                return ts.Seconds == 1 ? "1 sec ago" : ts.Seconds + " secs ago";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes == 1 ? "1 min ago" : ts.Minutes + " mins ago";
            }
            if (delta < 86400)  // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours == 1 ? "1 hour ago" : ts.Hours + " hours ago";
            }

            int days = ts.Days;
            if (days == 1) {
                return "yesterday";
            } else if (days <= 2) {
                return days + " days ago";
            } else if (utcNow.Year == dt.Year) {
                return dt.ToString("MMM %d 'at' %H:mmm");
            }
            return dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
        }

        private static string ToRelativeTimeFuture(DateTime dt, DateTime utcNow) {
            TimeSpan ts = dt - utcNow;
            double delta = ts.TotalSeconds;

            if (delta < 60) {
                return ts.Seconds == 1 ? "in 1 second" : "in " + ts.Seconds + " seconds";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes == 1 ? "in 1 minute" : "in " + ts.Minutes + " minutes";
            }
            if (delta < 86400) // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours == 1 ? "in 1 hour" : "in " + ts.Hours + " hours";
            }

            // use our own rounding so we can round the correct direction for future
            int days = (int)Math.Round(ts.TotalDays, 0);
            if (days == 1) {
                return "tomorrow";
            } else if (days <= 10) {
                return "in " + days + " day" + (days > 1 ? "s" : "");
            }
            // if the date is in the future enough to be in a different year, display the year
            if (utcNow.Year != dt.Year)
                return "on " + dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
            else
                return "on " + dt.ToString("MMM %d 'at' %H:mmm");

        }

        /// <summary>
        /// returns a html span element with relative time elapsed since this event occurred, eg, "3 months ago" or "yesterday"; 
        /// assumes time is *already* stored in UTC format!
        /// </summary>
        public static string ToRelativeTimeSpan(this DateTime dt) {
            return ToRelativeTimeSpan(dt, "relativetime");
        }
        public static string ToRelativeTimeSpan(this DateTime dt, string cssclass) {
            if (cssclass == null)
                return string.Format(@"<span title=""{0:u}"">{1}</span>", dt, ToRelativeTime(dt));
            else
                return string.Format(@"<span title=""{0:u}"" class=""{2}"">{1}</span>", dt, ToRelativeTime(dt), cssclass);
        }
        public static string ToRelativeTimeSpan(this DateTime? dt) {
            if (dt == null) return "";
            return ToRelativeTimeSpan(dt.Value);
        }


        /// <summary>
        /// returns a very *small* humanized string indicating how long ago something happened, eg "3d ago"
        /// </summary>
        public static string ToRelativeTimeMini(this DateTime dt) {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - dt.Ticks);
            double delta = ts.TotalSeconds;

            if (delta < 60) {
                return ts.Seconds + "s ago";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes + "m ago";
            }
            if (delta < 86400)  // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours + "h ago";
            }
            int days = ts.Days;
            if (days <= 2) {
                return days + "d ago";
            } else if (days <= 330) {
                return dt.ToString("MMM %d 'at' %H:mmm").ToLowerInvariant();
            }
            return dt.ToString(@"MMM %d \'yy 'at' %H:mmm").ToLowerInvariant();
        }


        public static string ToRelativeTimeMicro(this DateTime dt) {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - dt.Ticks);
            
            if (ts.Days <= 330) {
                return dt.ToString("MMM %d").ToLower(); 
            } else  {
                return dt.ToString("MMM %d yy").ToLower();
            } 
        }

        /// <summary>
        /// returns AN HTML SPAN ELEMENT with minified relative time elapsed since this event occurred, eg, "3mo ago" or "yday"; 
        /// assumes time is *already* stored in UTC format!
        /// </summary>
        public static string ToRelativeTimeSpanMini(this DateTime dt) {
            return string.Format(@"<span title=""{0:u}"" class=""relativetime"">{1}</span>", dt, ToRelativeTimeMini(dt));
        }
        /// <summary>
        /// returns AN HTML SPAN ELEMENT with minified relative time elapsed since this event occurred, eg, "3mo ago" or "yday"; 
        /// assumes time is *already* stored in UTC format!
        /// If this DateTime? is null, will return empty string.
        /// </summary>
        public static string ToRelativeTimeSpanMini(this DateTime? dt) {
            if (dt == null) return "";
            return ToRelativeTimeSpanMini(dt.Value);
        }

        public static string ToRelativeTimeSpanMicro(this DateTime dt) {
            return string.Format(@"<span title=""{0:u}"" class=""relativetime"">{1}</span>", dt, ToRelativeTimeMicro(dt));
        }

        public static string ToRelativeTimeSpanMicro(this DateTime? dt) {
            if (dt == null) return "";
            return ToRelativeTimeSpanMicro(dt.Value);
        }


        public static string ToAtomFeedDate(this DateTime dt) {
            return string.Format("{0:yyyy-MM-ddTHH:mm:ssZ}", dt);
        }
        public static string ToAtomFeedDate(this DateTime? dt) {
            return dt == null ? "" : ToAtomFeedDate(dt.Value);
        }


        /// <summary>
        /// returns how long something took in sec, minutes, hours, or days
        /// </summary>
        public static string TimeTaken(this TimeSpan time) {
            string output = "";
            if (time.Days > 0)
                output += time.Days + " day" + (time.Days > 1 ? "s " : " ");
            if ((time.Days == 0 || time.Days == 1) && time.Hours > 0)
                output += time.Hours + " hour" + (time.Hours > 1 ? "s " : " ");
            if (time.Days == 0 && time.Minutes > 0)
                output += time.Minutes + " minute" + (time.Minutes > 1 ? "s " : " ");
            if (output.Length == 0)
                output += time.Seconds + " second" + (time.Seconds > 1 ? "s " : " ");
            return output.Trim();
        }
        /// <summary>
        /// returns how long something took in years, months, or days
        /// </summary>
        public static string TimeTakenLong(this DateTime dt) {
            int days = (DateTime.UtcNow - dt).Days;
            if (days <= 0)
                return "today";
            if (days <= 1)
                return "yesterday";
            if (days > 365) {
                return (days / 365) + " year" + ((days / 365) > 1 ? "s ago" : " ago");
            }
            if (days > 30) {
                return (days / 30) + " month" + ((days / 30) > 1 ? "s ago" : " ago");
            }
            return days + " day" + (days > 1 ? "s ago" : " ago");
        }
        /// <summary>
        /// returns how long something took in years, months, or days
        /// </summary>
        public static string TimeTakenLong(this DateTime? dt) {
            if (dt == null) return "";
            return TimeTakenLong(dt.Value);
        }


        private static Crc16 _crc16 = new Crc16();

        public static string ToCrc16(this string s) {
            if (s.IsNullOrEmpty()) return "";

            byte[] crc = _crc16.ComputeChecksumBytes(Encoding.UTF8.GetBytes(s));
            return crc[0].ToString("x2") + crc[1].ToString("x2");
        }

        public static string ToMD5Hash(this string s) {
            return ToHash(() => MD5.Create(), s);
        }

        public static string ToSha256Hash(this string s) {
            return ToHash(() => SHA256.Create(), s);
        }

        private static string ToHash(Func<HashAlgorithm> createMethod, string toHash) {
            if (toHash.IsNullOrEmpty()) return "";

            byte[] hash;
            using (var algorithm = createMethod()) {
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(toHash));
            }
            // hex encoding yields 2 char/byte -> 255 == FF == 1111 1111
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) {
                result.Append(b.ToString("x2"));
            }

            return result.ToString();
        }


        public static string Encrypt(this string toEncrypt, string key, bool useHashing) {

            byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
            byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);

            if (useHashing)
                keyArray = new MD5CryptoServiceProvider().ComputeHash(keyArray);

            var tdes = new TripleDESCryptoServiceProvider() { Key = keyArray, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 };
            ICryptoTransform cTransform = tdes.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            return Convert.ToBase64String(resultArray, 0, resultArray.Length);
        }

        public static string Decrypt(this string toDecrypt, string key, bool useHashing) {
            try {
                byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
                byte[] toEncryptArray = Convert.FromBase64String(toDecrypt);

                if (useHashing)
                    keyArray = new MD5CryptoServiceProvider().ComputeHash(keyArray);

                var tdes = new TripleDESCryptoServiceProvider() { Key = keyArray, Mode = CipherMode.ECB, Padding = PaddingMode.PKCS7 };
                ICryptoTransform cTransform = tdes.CreateDecryptor();
                byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

                return UTF8Encoding.UTF8.GetString(resultArray);
            } catch {
                // Decrypting failed 
                return string.Empty;
            }
        }

        public static string Pluralize(this string word, int number) {
            if (number == 1) {
                return word;
            } else {
                return Inflector.MakePlural(word);
            }
        }

        public static string PrettyShort(this System.Int32? num) {
            if (num == null) { return ""; }
            string rval;

            if (num < 1000) {
                rval = num.ToString();
            } else {

                var divisor = num < 1000000 ? 1000.0 : 1000000.0;

                if (((int)Math.Round(num.Value / divisor)).ToString().Length > 1) {
                    rval = (Math.Round(num.Value / divisor)).ToString();
                } else {
                    rval = (Math.Round(num.Value / (divisor / 10.0)) / 10.0).ToString();
                }
            } 

            if (rval.Length > 3) {
                rval = rval.Substring(0, 3);
            }

            while (rval.Contains(".") && rval.Last() == '0') {
                rval = rval.Substring(0, rval.Length - 1);
            }

            rval = rval.Last() == '.' ? rval.Substring(0, rval.Length - 1) : rval;

            string suffix = "";
            if (num > 999 && num < 1000000) {
                suffix = "k";
            } else {
                suffix = "m";
            }

            return string.Format("<span class=\"pretty-short\" title=\"{0}\">{1}{2}</span>", Pretty(num), rval, suffix);
            
        }

        public static string Pretty(this System.Int32? num){
            if (num == 0) return "";
            return num.Value.ToString("#,##0");
        }

        /// <summary>
        /// renders a user control as a plain vanilla string
        /// </summary>
        public static string RenderControl(this System.Web.UI.Control control) {
            StringBuilder sb = new StringBuilder();
            using (var sw = new System.IO.StringWriter(sb)) {
                using (var textWriter = new System.Web.UI.HtmlTextWriter(sw)) {
                    control.RenderControl(textWriter);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// compresses UTF-8 string into byte[] using GZip
        /// </summary>
        public static byte[] Compress(string s) {
            byte[] b;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8)) {
                b = Encoding.UTF8.GetBytes(s);
                // prepend the compressed data with the length of the uncompressed data (first 4 bytes)
                writer.Write(b.Length);
                using (var st = new DeflateStream(ms, CompressionMode.Compress)) {
                    st.Write(b, 0, b.Length);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// decompress byte[] to UTF-8 string using GZip
        /// </summary>
        public static string Decompress(byte[] compressedbytes) {
            using (MemoryStream ms = new MemoryStream(compressedbytes))
            using (BinaryReader reader = new BinaryReader(ms)) {
                // read final uncompressed string size stored in first 4 bytes
                int size = reader.ReadInt32();
                using (var st = new DeflateStream(ms, CompressionMode.Decompress)) {
                    var b = new byte[size];
                    st.Read(b, 0, b.Length);
                    return Encoding.UTF8.GetString(b);
                }
            }
        }

        private static Regex _guidFormat = new Regex(
                @"^[A-Fa-f0-9]{32}$|
                  ^({|\()?[A-Fa-f0-9]{8}-([A-Fa-f0-9]{4}-){3}[A-Fa-f0-9]{12}(}|\))?$|
                  ^({)?[0xA-Fa-f0-9]{3,10}(, {0,1}[0xA-Fa-f0-9]{3,6}){2}, {0,1}({)([0xA-Fa-f0-9]{3,4}, {0,1}){7}[0xA-Fa-f0-9]{3,4}(}})$",
                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static bool GuidTryParse(this string s, out Guid result) {
            if (s == null) throw new ArgumentNullException("s");
            if (_guidFormat.IsMatch(s)) {
                result = new Guid(s);
                return true;
            } else {
                result = Guid.Empty;
                return false;
            }
        }

    }
}