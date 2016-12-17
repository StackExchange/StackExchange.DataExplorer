using System;
using System.Security.Cryptography;
using System.Text;

namespace StackExchange.DataExplorer.Helpers
{
    public static class Util
    {
        private static readonly MD5CryptoServiceProvider _md5Provider = new MD5CryptoServiceProvider();

        public static Guid GetMD5(string str)
        {
            lock (_md5Provider)
            {
                return new Guid(_md5Provider.ComputeHash(Encoding.Unicode.GetBytes(str)));
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

        public static string GravatarHash(string str) => GetMD5String(str.ToLower().Trim()).ToLower();

        public static DateTime FromJavaScriptTime(long milliseconds) => 
            new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(milliseconds);
    }
}