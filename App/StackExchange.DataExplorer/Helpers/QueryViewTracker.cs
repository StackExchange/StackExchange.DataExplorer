using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;

namespace StackExchange.DataExplorer.Helpers {
    public class QueryViewTracker {

        const int VIEW_EXPIRES_SECS = 15 * 60; // view information expires in 15 minutes

        // TODO: batch this up 
        public static void TrackQueryView(string ipAddress, int queryId) {
            if (IsNewView(ipAddress, queryId)) {
                Current.DB.ExecuteCommand("update Queries set Views = Views + 1 where Id = " + queryId.ToString());
            }
        }

        public static bool IsNewView(string ipAddress, int queryId) {
            string key = "qv - " + ipAddress + " " + queryId.ToString();
            bool isNewView = true;

            int currentBracket = GetTimeBracket();

            var cached = HttpRuntime.Cache.Get(key);
            if (cached != null) {
                var cachedBracket = (int)cached;
                if (cachedBracket == currentBracket || cachedBracket == (currentBracket - 1)) {
                    isNewView = false;
                }
            }

            HttpRuntime.Cache.Insert(
                key,
                currentBracket,
                null,
                Cache.NoAbsoluteExpiration,
                new TimeSpan(0, 0, VIEW_EXPIRES_SECS),
                CacheItemPriority.High,
                null
            );

            return isNewView;
        }


        private static int GetTimeBracket() {
            return Convert.ToInt32(DateTime.UtcNow.Ticks.ToString().Substring(0, 8));
        }


    }
}