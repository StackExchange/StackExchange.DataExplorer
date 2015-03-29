using System;
using System.Security.Cryptography;
using System.Text;

namespace StackExchange.DataExplorer.Helpers
{
    public static class Util
    {
        private static readonly MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider();

        public static Guid GetMD5(string str)
        {
            lock (md5Provider)
            {
                return new Guid(md5Provider.ComputeHash(Encoding.Unicode.GetBytes(str)));
            }
        }

        private static string GetMD5String(string value)
        {
            using (var md5 = new MD5CryptoServiceProvider())
            {
                var e = new UTF8Encoding();
                var sb = new StringBuilder(32);
                var b = md5.ComputeHash(e.GetBytes(value));
                for (int i = 0; i < b.Length; i++)
                    sb.Append(b[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string GravatarHash(string str)
        {
            return GetMD5String(str.ToLower().Trim()).ToLower();
        }

        public static DateTime FromJavaScriptTime(long milliseconds)
        {
            return (new DateTime(1970, 1, 1, 0, 0, 0, 0)).AddMilliseconds(milliseconds);
        }
    }
}