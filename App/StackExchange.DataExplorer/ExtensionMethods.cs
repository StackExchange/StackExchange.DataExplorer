using System;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// returns a hash of the time that is stable for up to 16 minutes (999 seconds)
        /// </summary>
        public static string GetHashedTime()
        {
            // one tick is 10ns; ticks are 18 characters long
            return ("ticks=" + DateTime.UtcNow.Ticks.ToString().Substring(0, 8)).ToCrc16();
        }

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

        /// <summary>
        /// force string to be maxlen or smaller
        /// </summary>
        public static string Truncate(this string s, int maxLength)
        {
            if (s.IsNullOrEmpty()) return s;
            return s.Length > maxLength ? s.Remove(maxLength) : s;
        }

        public static string TruncateWithEllipsis(this string s, int maxLength)
        {
            if (s.IsNullOrEmpty()) return s;
            if (s.Length <= maxLength) return s;

            return Truncate(s, maxLength - 3) + "...";
        }

        /// <summary>
        /// Produces a URL-friendly version of this String, "like-this-one".
        /// </summary>
        public static string URLFriendly(this string s) => s.HasValue() ? HtmlUtilities.URLFriendly(s) : s;

        /// <summary>
        /// Produces a URL-friendly version of this String, "like-this-one", and prepends it with
        /// a forward slash if the URL-friendly version is non-blank
        /// </summary>
        public static string Slugify(this string s)
        {
            if (!s.HasValue()) return s;

            var slug = HtmlUtilities.URLFriendly(s);

            return slug.HasValue() ? "/" + slug : "";
        }

        public static string Slugify(this QuerySet metadata)
        {
            if (metadata == null || metadata.Title.IsNullOrEmpty())
            {
                return "";
            }

            return "/" + metadata.Title.URLFriendly();
        }

        /// <summary>
        /// returns Url Encoded string
        /// </summary>
        public static string UrlEncode(this string s) => s.HasValue() ? HttpUtility.UrlEncode(s) : s;
        
        /// <summary>
        /// Adds the url to the RouteCollection with the specified defaults.
        /// </summary>
        /// <remarks>Added to remove annoying empty space in all MapRoute calls.</remarks>
        public static void MapRoute(this RouteCollection routes, string url, object defaults) => routes.MapRoute("", url, defaults);

        public static string Temperature(this DateTime dt)
        {
            var delta = (DateTime.UtcNow - dt).TotalMinutes;

            if (delta < 10)
            {
                return "supernova";
            }
            if (delta < 120)
            {
                return "warm";
            }

            return "cool";
        }

        public static string Temperature(this DateTime? dt) => dt == null ? "cool" : Temperature(dt.Value);

        /// <summary>
        /// Returns a unix Epoch time given a Date
        /// </summary>
        public static long ToJavascriptTime(this DateTime dt)
        {
            return (long) (dt - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds;
        }

        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// </summary>
        public static string ToRelativeTime(this DateTime dt)
        {
            var utcNow = DateTime.UtcNow;
            return dt <= utcNow ? ToRelativeTimePast(dt, utcNow) : ToRelativeTimeFuture(dt, utcNow);
        }

        /// <summary>
        /// Returns a humanized string indicating how long ago something happened, eg "3 days ago".
        /// For future dates, returns when this DateTime will occur from DateTime.UtcNow.
        /// If this DateTime is null, returns empty string.
        /// </summary>
        public static string ToRelativeTime(this DateTime? dt) => dt == null ? "" : ToRelativeTime(dt.Value);

        private static string ToRelativeTimePast(DateTime dt, DateTime utcNow)
        {
            TimeSpan ts = utcNow - dt;
            double delta = ts.TotalSeconds;

            if (delta < 60)
            {
                return ts.Seconds == 1 ? "1 sec ago" : ts.Seconds + " secs ago";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes == 1 ? "1 min ago" : ts.Minutes + " mins ago";
            }
            if (delta < 86400) // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours == 1 ? "1 hour ago" : ts.Hours + " hours ago";
            }

            var days = ts.Days;
            if (days == 1)
            {
                return "yesterday";
            }
            if (days <= 2)
            {
                return days + " days ago";
            }
            if (utcNow.Year == dt.Year)
            {
                return dt.ToString("MMM %d 'at' %H:mmm");
            }
            return dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
        }

        private static string ToRelativeTimeFuture(DateTime dt, DateTime utcNow)
        {
            var ts = dt - utcNow;
            var delta = ts.TotalSeconds;

            if (delta < 60)
            {
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
            var days = (int) Math.Round(ts.TotalDays, 0);
            if (days == 1)
            {
                return "tomorrow";
            }
            if (days <= 10)
            {
                return "in " + days + " day" + (days > 1 ? "s" : "");
            }
            // if the date is in the future enough to be in a different year, display the year
            if (utcNow.Year != dt.Year)
                return "on " + dt.ToString(@"MMM %d \'yy 'at' %H:mmm");
            return "on " + dt.ToString("MMM %d 'at' %H:mmm");
        }

        /// <summary>
        /// returns a html span element with relative time elapsed since this event occurred, eg, "3 months ago" or "yesterday"; 
        /// assumes time is *already* stored in UTC format!
        /// </summary>
        public static string ToRelativeTimeSpan(this DateTime dt) => ToRelativeTimeSpan(dt, "relativetime");

        public static string ToRelativeTimeSpan(this DateTime dt, string cssclass)
        {
            if (cssclass == null)
                return $@"<span title=""{dt:u}"">{ToRelativeTime(dt)}</span>";
            return $@"<span title=""{dt:u}"" class=""{cssclass}"">{ToRelativeTime(dt)}</span>";
        }

        public static string ToRelativeTimeSpan(this DateTime? dt) => dt == null ? "" : ToRelativeTimeSpan(dt.Value);
        
        /// <summary>
        /// returns a very *small* humanized string indicating how long ago something happened, eg "3d ago"
        /// </summary>
        public static string ToRelativeTimeMini(this DateTime dt)
        {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - dt.Ticks);
            var delta = ts.TotalSeconds;

            if (delta < 60)
            {
                return ts.Seconds + "s ago";
            }
            if (delta < 3600) // 60 mins * 60 sec
            {
                return ts.Minutes + "m ago";
            }
            if (delta < 86400) // 24 hrs * 60 mins * 60 sec
            {
                return ts.Hours + "h ago";
            }
            int days = ts.Days;
            if (days <= 2)
            {
                return days + "d ago";
            }
            if (days <= 330)
            {
                return dt.ToString("MMM %d 'at' %H:mmm").ToLowerInvariant();
            }
            return dt.ToString(@"MMM %d \'yy 'at' %H:mmm").ToLowerInvariant();
        }


        public static string ToRelativeTimeMicro(this DateTime dt)
        {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - dt.Ticks);

            if (ts.Days <= 330)
            {
                return dt.ToString("MMM %d").ToLower();
            }
            return dt.ToString("MMM %d yy").ToLower();
        }

        /// <summary>
        /// returns AN HTML SPAN ELEMENT with minified relative time elapsed since this event occurred, eg, "3mo ago" or "yday"; 
        /// assumes time is *already* stored in UTC format!
        /// </summary>
        public static string ToRelativeTimeSpanMini(this DateTime dt) => $@"<span title=""{dt:u}"" class=""relativetime"">{ToRelativeTimeMini(dt)}</span>";
        
        public static IHtmlString AsHtml(this string html) => MvcHtmlString.Create(html);

        public static IHtmlString ToRelativeTimeSpanMicro(this DateTime dt) => 
            $@"<span title=""{dt:u}"" class=""relativetime"">{ToRelativeTimeMicro(dt)}</span>".AsHtml();

        public static IHtmlString ToRelativeTimeSpanMicro(this DateTime? dt) => dt == null ? "".AsHtml() : ToRelativeTimeSpanMicro(dt.Value);
        
        /// <summary>
        /// returns how long something took in sec, minutes, hours, or days
        /// </summary>
        public static string TimeTaken(this TimeSpan time)
        {
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
        public static string TimeTakenLong(this DateTime dt)
        {
            int days = (DateTime.UtcNow - dt).Days;
            if (days <= 0)
                return "today";
            if (days <= 1)
                return "yesterday";
            if (days > 365)
            {
                return (days/365) + " year" + ((days/365) > 1 ? "s ago" : " ago");
            }
            if (days > 30)
            {
                return (days/30) + " month" + ((days/30) > 1 ? "s ago" : " ago");
            }
            return days + " day" + (days > 1 ? "s ago" : " ago");
        }

        private static readonly Crc16 _crc16 = new Crc16();
        public static string ToCrc16(this string s)
        {
            if (s.IsNullOrEmpty()) return "";

            var crc = _crc16.ComputeChecksumBytes(Encoding.UTF8.GetBytes(s));
            return crc[0].ToString("x2") + crc[1].ToString("x2");
        }

        public static string ToMD5Hash(this string s) => ToHash(MD5.Create, s);
        
        private static string ToHash(Func<HashAlgorithm> createMethod, string toHash)
        {
            if (toHash.IsNullOrEmpty()) return "";

            byte[] hash;
            using (var algorithm = createMethod())
            {
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(toHash));
            }
            // hex encoding yields 2 char/byte -> 255 == FF == 1111 1111
            var result = new StringBuilder(hash.Length*2);
            foreach (var b in hash)
            {
                result.Append(b.ToString("x2"));
            }

            return result.ToString();
        }

        // http://meta.stackexchange.com/questions/61380/inflector-net-not-correctly-attributed-to-andrew-peters-in-stack-exchange-data-ex
        public static string Pluralize(this string word, int number) => number == 1 ? word : word + "s";

        public static string PrettyShort(this int? num) => num == null ? "" : num.Value.PrettyShort();

        public static string PrettyShort(this int num)
        {
            string rval;

            if (num < 1000)
            {
                rval = num.ToString();
            }
            else
            {
                double divisor = num < 1000000 ? 1000.0 : 1000000.0;

                if (((int) Math.Round(num/divisor)).ToString().Length > 1)
                {
                    rval = (Math.Round(num/divisor)).ToString();
                }
                else
                {
                    rval = (Math.Round(num/(divisor/10.0))/10.0).ToString();
                }
            }

            if (rval.Length > 3)
            {
                rval = rval.Substring(0, 3);
            }

            while (rval.Contains(".") && rval.Last() == '0')
            {
                rval = rval.Substring(0, rval.Length - 1);
            }

            rval = rval.Last() == '.' ? rval.Substring(0, rval.Length - 1) : rval;

            string suffix = "";
            if (num < 1000) {
                suffix = "";
            }
            else if (num > 999 && num < 1000000)
            {
                suffix = "k";
            }
            else if (num >= 1000000) 
            {
                suffix = "m";
            }

            return $"<span class=\"pretty-short\" title=\"{Pretty(num)}\">{rval}{suffix}</span>";
        }

        public static string Pretty(this int? num) => num == 0 ? "" : num.Value.ToString("#,##0");

        #region NameValueCollection (aka Request.Form) Get Helpers
        
        /// <summary>
        /// Answers true if a value is found for 'key'
        /// </summary>
        public static bool Contains(this NameValueCollection nvc, string key) => !string.IsNullOrEmpty(nvc[key]);

        public static T Get<T>(this NameValueCollection nvc, string key, T defaultValue) => Get(nvc, key, defaultValue, false);

        private static T Get<T>(NameValueCollection nvc, string key, T defaultValue, bool throwExceptionWhenValueIsEmpty)
        {
            T result = defaultValue;
            Type resultType = typeof (T);

            string value = nvc[key];

            if (string.IsNullOrEmpty(value))
            {
                if (throwExceptionWhenValueIsEmpty)
                {
                    throw new HttpRequestValidationException($"Unable to find a Form value for key '{key}'.");
                }
            }
            else
            {
                try
                {
                    // Some controls submit "on" when checked or selected..
                    if (resultType == typeof (bool) && value.Equals("on", StringComparison.OrdinalIgnoreCase))
                    {
                        value = "True";
                    }

                    if (resultType == typeof (string))
                    {
                        // don't trim strings here; this breaks markdown parsing, as leading spaces can be significant
                        result = (T) Convert.ChangeType(HtmlUtilities.RemoveInvalidXMLChars(value), resultType);
                    }
                    else if (resultType.IsEnum)
                    {
                        result = (T) Enum.Parse(resultType, value);
                    }
                    else
                    {
                        result = (T) Convert.ChangeType(value.Trim(), resultType);
                    }
                }
                catch (Exception ex)
                {
                    throw new HttpRequestValidationException(
                        $"Encountered a problem trying to convert '{value ?? "NULL"}' to a {resultType.FullName}.",
                                                             ex);
                }
            }

            return result;
        }

        #endregion

        public static void SetPageTitle(this WebViewPage page, string title)
        {
            page.ViewData["PageTitle"] = HtmlUtilities.Encode(title);
        }

        public static string ReplaceFirst(this string input, string search, string replace)
        {
            var index = input.IndexOf(search);
            if (index < 0)
            {
                return input;
            }
            return input.Substring(0, index) + replace + input.Substring(index + search.Length);
        }
    }
}