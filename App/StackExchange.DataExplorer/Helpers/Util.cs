using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.Security;

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

        public static string GravatarHash(string str)
        {
            return FormsAuthentication.HashPasswordForStoringInConfigFile(str.ToLower().Trim(), "MD5").ToLower();
        }
    }
}