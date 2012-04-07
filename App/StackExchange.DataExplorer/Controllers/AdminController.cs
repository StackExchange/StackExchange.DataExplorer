using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using System.Linq;

namespace StackExchange.DataExplorer.Controllers
{
    using user2user = Func<IEnumerable<User>, IEnumerable<User>>;
    using whitelist2whitelist = Func<IEnumerable<OpenIdWhiteList>, IEnumerable<OpenIdWhiteList>>;

    public class AdminController : StackOverflowController
    {

        protected override void OnActionExecuting(ActionExecutingContext c)
        {
            // all actions in this controller are admin only
            if (!Allowed()) c.Result = TextPlainNotFound();

            base.OnActionExecuting(c);
        }

        [Route("admin/whitelist/approve/{id:int}", HttpVerbs.Post)]
        public ActionResult ApproveWhiteListEntry(int id)
        {
            Current.DB.OpenIdWhiteList.Update(id, new { Approved = true });
            return Json("ok");
        }

        [Route("admin/whitelist/remove/{id:int}", HttpVerbs.Post)]
        public ActionResult RemoveWhiteListEntry(int id)
        {
            Current.DB.OpenIdWhiteList.Delete(id);
            return Json("ok");
        }

        [Route("admin/whitelist")]
        public ActionResult WhiteList()
        {
            SetHeader("Open Id Whitelist");

            return View(Current.DB.OpenIdWhiteList.All()); 
        }

        [Route("admin")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("admin/clear-table-cache")]
        public ActionResult ClearTableCache()
        {
            HelperTableCache.Refresh();

            return Redirect("/admin");
        }

        [Route("admin/clear-cache")]
        public ActionResult ClearCache()
        {
            PurgeCache();

            return Redirect("/admin");
        }

        [Route("admin/refresh-stats", HttpVerbs.Post)]
        public ActionResult RefreshStats()
        {
            foreach (Site site in Current.DB.Sites.All())
            {
                site.UpdateStats();
            }

            PurgeCache();

            return Content("sucess");
        }

        private void PurgeCache()
        {
            Current.DB.Execute("truncate table CachedResults");
            Current.DB.Execute("truncate table CachedPlans");
            HelperTableCache.Refresh();
        }

        private Dictionary<string, user2user> userSorts = new Dictionary<string, user2user>()
        {
            { "oldest", users => users.OrderBy(u => u.CreationDate) },
            { "last-seen", users => users.OrderByDescending(u => u.LastSeenDate) },
            { "last-active", users => users.OrderByDescending(u => u.LastActivityDate) }
        };

        private Dictionary<string, whitelist2whitelist> whitelistSorts = new Dictionary<string, whitelist2whitelist>()
        {
            { "approved", whitelist => whitelist.OrderByDescending(w => w.Approved) },
            { "oldest", whitelist => whitelist.OrderBy(w => w.CreationDate) },
            { "newest", whitelist => whitelist.OrderByDescending(w => w.CreationDate) }
        };

        [Route("admin/find-dupe-users")]
        public ActionResult FindDuplicateUsers(string sort, bool useEmail = false)
        {
            var sorter = userSorts[sort ?? "oldest"];


            List<Tuple<string, IEnumerable<int>>> dupeUserIds = null; 
                
          

            if (useEmail)
            {
                var allUsers = Current.DB.Query(@"select Email, Id from Users
where Email is not null and len(rtrim(Email)) > 0 ");

                dupeUserIds = (from email in allUsers
                               group email by (string)email.Email
                                   into grp
                                   where grp.Count() > 1
                                   select new Tuple<string, IEnumerable<int>>(grp.Key, grp.Select(u => (int)u.Id).OrderBy(id => id).ToList())).ToList();
            }
            else
            {
                var openids = Current.DB.Query<UserOpenId>("select * from UserOpenIds").ToList();
                dupeUserIds = (from openid in openids
                               group openid by Models.User.NormalizeOpenId(openid.OpenIdClaim)
                                   into grp
                                   where grp.Count() > 1
                                   select new Tuple<string, IEnumerable<int>>(grp.Key, grp.Select(id => id.UserId).OrderBy(id => id))).ToList();
            }

            ViewBag.Sort = sort;
            ViewBag.Sorts = userSorts.Keys;
            SetHeader("Possible Duplicate Users");

            if (dupeUserIds.Count() > 0)
            {

                var userMap = Current.DB.Query<User>("select * from Users where Id in @Ids", new { Ids = dupeUserIds.Select(u => u.Item2).SelectMany(u => u) })
                    .ToDictionary(u => u.Id);

                var dupeUsers = dupeUserIds.Select(tuple => Tuple.Create(tuple.Item1, tuple.Item2.Select(id => userMap[id]))).ToList();

                return View(dupeUsers);
            }
            else
            {
                return View((object)null);
            }

        }

        [Route("admin/normalize-openids")]
        public ActionResult NormalizeOpenIds()
        {
            foreach (var openId in Current.DB.UserOpenIds.All())
            {
                var cleanClaim = Models.User.NormalizeOpenId(openId.OpenIdClaim);
                if (cleanClaim != openId.OpenIdClaim)
                {
                    Current.DB.UserOpenIds.Update(openId.Id, new { OpenIdClaim = cleanClaim });
                }
            }
            return TextPlain("Done.");
        }

        [Route("admin/find-dupe-whitelist-openids")]
        public ActionResult FindDuplicateWhitelistOpenIds(string sort)
        {
            var sorter = whitelistSorts[sort ?? "approved"];
            var whitelistOpenIds = Current.DB.OpenIdWhiteList.All().ToList();
            var dupeOpenIds = (from openid in whitelistOpenIds
                               group openid by Models.User.NormalizeOpenId(openid.OpenId)
                                   into grp
                                   where grp.Count() > 1
                                   select new Tuple<string, IEnumerable<OpenIdWhiteList>>(grp.Key, sorter(grp.Select(id => id)))).
                ToList();
            ViewBag.Sort = sort;
            ViewBag.Sorts = whitelistSorts.Keys;

            SetHeader("Possible Duplicate Whitelist OpenIds");
            return View(dupeOpenIds);
        }

        [Route("admin/normalize-whitelist-openids")]
        public ActionResult NormalizeWhitelistOpenIds()
        {
            foreach (var openid in Current.DB.Query<OpenIdWhiteList>("select * from OpenIdWhiteList"))
            {
                var newOpenId = Models.User.NormalizeOpenId(openid.OpenId);
                if (openid.OpenId != newOpenId)
                {
                    Current.DB.OpenIdWhiteList.Update(openid.Id, new { OpenId = newOpenId });
                }
            }
            return TextPlain("Done.");
        }

        [Route("admin/merge-users", HttpVerbs.Get)]
        public ActionResult MergeUsers(int masterId, int mergeId)
        {
            var canMergeMsg = "";
            if (!Models.User.CanMergeUsers(masterId, mergeId, out canMergeMsg))
            {
                return TextPlain(canMergeMsg);
            }
            ViewBag.MasterUser = Current.DB.Users.Get(masterId);
            ViewBag.MergeUser = Current.DB.Users.Get(mergeId);
            SetHeader("Merge Users");
            return View();
        }

        [Route("admin/merge-users-submit", HttpVerbs.Post)]
        public ActionResult MergeSubmit(int masterId, int mergeId)
        {
            var log = new StringBuilder();
            Models.User.MergeUsers(masterId, mergeId, log);
            return TextPlain(log.ToString());
        }

        [Route("admin/find-dupe-user-openids")]
        public ActionResult FindDuplicateUserOpenIds()
        {

            var sql = "select * from UserOpenIds where UserId in (select UserId from UserOpenId having count(*) > 0)";

            var dupes = (from uoi in Current.DB.Query<UserOpenId>(sql)
                        group uoi by uoi.UserId
                        into grp
                        select new Tuple<int?, IEnumerable<UserOpenId>>(grp.Key, grp.Select(g=>g))).ToList();
            SetHeader("Possible Duplicate User OpenId records");
            return View(dupes);
        }


        [Route("admin/useropenid/remove/{id:int}", HttpVerbs.Post)]
        public ActionResult RemoveUserOpenIdEntry(int id)
        {
            Current.DB.UserOpenIds.Delete(id);
            return Json("ok");
        }


        public bool Allowed()
        {
            return CurrentUser.IsAdmin;
        }

    }
}