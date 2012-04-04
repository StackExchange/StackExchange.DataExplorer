using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using System.Net;
using System.Collections.Concurrent;
using System.IO;

// little cache for favicons 

namespace StackExchange.DataExplorer.Controllers
{
    public class IconController : StackOverflowController
    {
        class CacheInfo
        {
            public DateTime CacheDate { get; set; }
            public byte[] Image { get; set; }
        }

        static ConcurrentDictionary<int, CacheInfo> icons = new ConcurrentDictionary<int, CacheInfo>();

        [Route("icon/{id:INT}")]
        public ActionResult GetIcon(int id)
        {
            var s = Current.DB.Sites.Get(id);

            if (s != null && !s.IconUrl.IsNullOrEmpty())
            {
                var icon = GetCachedIcon(s);
                if (icon != null)
                {
                    Response.AddHeader("Cache-Control", "max-age=604800");
                    var ms = new MemoryStream(icon);
                    ms.Seek(0, SeekOrigin.Begin);
                    return new FileStreamResult(ms, "image/x-icon");
                }
            }
 

            Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Content(null);
        }

        private static byte[] GetCachedIcon(Models.Site s)
        {
            CacheInfo rval;
            if (icons.TryGetValue(s.Id, out rval))
            {
                if (DateTime.UtcNow.AddMinutes(-720) < rval.CacheDate)
                {
                    return rval.Image;
                }
            }

            rval = new CacheInfo { CacheDate = DateTime.UtcNow };
            try 
            {
                lock(icons)
                {
                    using (var client = new WebClient())
                    {
                        var stream = client.OpenRead(s.IconUrl);
                        rval.Image = ReadFully(stream);
                        icons.TryAdd(s.Id, rval);
                    }
                    
                }
            }
            catch
            {
                icons.TryAdd(s.Id, rval);
            }
            return rval.Image;
        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

    }
}
