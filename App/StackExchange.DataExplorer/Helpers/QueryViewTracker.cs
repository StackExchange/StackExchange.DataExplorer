using System;
using System.Web;
using System.Web.Caching;

namespace StackExchange.DataExplorer.Helpers
{
    public class QueryViewTracker
    {
        private const int VIEW_EXPIRES_SECS = 15*60; // view information expires in 15 minutes

        // TODO: we may consider batching this up for performance sake
        public static void TrackQueryView(string ipAddress, int revisionId, int? ownerId)
        {
            if (IsNewView(ipAddress, revisionId, ownerId))
            {
                Current.DB.Execute(@"
                    UPDATE
                        Metadata
                    SET
                        Views = Views + 1
                    WHERE
                        RevisionId = @revision AND
                        OwnerId " + (ownerId.HasValue ? "= @owner" : "IS NULL"),
                    new
                    {
                        revision = revisionId,
                        owner = ownerId.HasValue ? ownerId.Value : -1
                    }
                );
            }
        }

        public static bool IsNewView(string ipAddress, int revisionId, int? ownerId)
        {
            string key = "qv - " + ipAddress + " " + (ownerId.HasValue ? ownerId.Value + " " : "") + revisionId;
            bool isNewView = true;

            int currentBracket = GetTimeBracket();

            object cached = HttpRuntime.Cache.Get(key);
            if (cached != null)
            {
                var cachedBracket = (int) cached;
                if (cachedBracket == currentBracket || cachedBracket == (currentBracket - 1))
                {
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


        private static int GetTimeBracket()
        {
            return Convert.ToInt32(DateTime.UtcNow.Ticks.ToString().Substring(0, 8));
        }
    }
}