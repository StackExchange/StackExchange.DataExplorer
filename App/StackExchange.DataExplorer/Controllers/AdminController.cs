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
            Current.DB.OpenIdWhiteLists.First(w => w.Id == id).Approved = true;
            Current.DB.SubmitChanges();

            return Json("ok");
        }

        [Route("admin/whitelist/remove/{id:int}", HttpVerbs.Post)]
        public ActionResult RemoveWhiteListEntry(int id)
        {
            var entry = Current.DB.OpenIdWhiteLists.First(w => w.Id == id);
            Current.DB.OpenIdWhiteLists.DeleteOnSubmit(entry);
            Current.DB.SubmitChanges();

            return Json("ok");
        }

        [Route("admin/whitelist")]
        public ActionResult WhiteList()
        {
            SetHeader("Open Id Whitelist");

            return View(Current.DB.OpenIdWhiteLists);
        }

        [Route("admin")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("admin/refresh_stats", HttpVerbs.Post)]
        public ActionResult RefreshStats()
        {
            foreach (Site site in Current.DB.Sites)
            {
                site.UpdateStats();
            }

            Current.DB.ExecuteCommand("DELETE FROM CachedResults");

            return Content("sucess");
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
        public ActionResult FindDuplicateUsers(string sort)
        {
            var sorter = userSorts[sort ?? "oldest"];
            var openids = Current.DB.UserOpenIds.ToList();
            var dupeUsers = (from openid in openids
                             group openid by Models.User.NormalizeOpenId(openid.OpenIdClaim)
                                 into grp
                                 where grp.Count() > 1
                                 select new Tuple<string, IEnumerable<User>>(grp.Key, sorter(grp.Select(id => id.User)))).ToList();
            ViewBag.Sort = sort;
            ViewBag.Sorts = userSorts.Keys;
            SetHeader("Possible Duplicate Users");
            return View(dupeUsers);
        }

        [Route("admin/normalize-openids")]
        public ActionResult NormalizeOpenIds()
        {
            foreach (var openId in Current.DB.UserOpenIds)
            {
                openId.OpenIdClaim = Models.User.NormalizeOpenId(openId.OpenIdClaim);
            }
            Current.DB.SubmitChanges();
            return TextPlain("Done.");
        }

        [Route("admin/find-dupe-whitelist-openids")]
        public ActionResult FindDuplicateWhitelistOpenIds(string sort)
        {
            var sorter = whitelistSorts[sort ?? "approved"];
            var whitelistOpenIds = Current.DB.OpenIdWhiteLists.ToList();
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
            foreach (var openid in Current.DB.OpenIdWhiteLists)
            {
                openid.OpenId = Models.User.NormalizeOpenId(openid.OpenId);
            }
            Current.DB.SubmitChanges();
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
            ViewBag.MasterUser = Current.DB.Query<User>("select * from Users where Id = @Id", new { Id = masterId }).First();
            ViewBag.MergeUser = Current.DB.Query<User>("select * from Users where Id = @Id", new { Id = mergeId }).First();
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

        public bool Allowed()
        {
            return CurrentUser.IsAdmin;
        }

    }
}