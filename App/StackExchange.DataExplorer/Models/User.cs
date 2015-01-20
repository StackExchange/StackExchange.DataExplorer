﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models
{
    public partial class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Email { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsAdmin { get; set; }
        public string IPAddress { get; set; }
        public DateTime? CreationDate { get; set; }
        public string AboutMe { get; set; }
        public string Website { get; set; }
        public string Location { get; set; }
        public DateTime? DOB { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public DateTime? LastSeenDate { get; set; }
        public string PreferencesRaw { get; set; }

        List<UserOpenId> _userOpenIds;
        public List<UserOpenId> UserOpenIds
        {
            get
            {
                _userOpenIds = _userOpenIds ?? Current.DB.Query<UserOpenId>("select * from UserOpenIds where UserId = @Id", new { Id }).ToList();
                return _userOpenIds;
            }
        }

        public const string emptyLogin = "jon.doe";

        /// <summary>
        /// Should be set when ControllerBase determines CurrentUser
        /// </summary>
        public string XSRFFormValue { get; set; }

        public bool IsAnonymous { get; set; }

        public string SafeAboutMe
        {
            get { return HtmlUtilities.Safe(HtmlUtilities.RawToCooked(AboutMe ?? "")); }
        }

        public string Age
        {
            get
            {
                if (DOB == null) return "";

                DateTime now = DateTime.Today;
                int age = now.Year - DOB.Value.Year;
                if (DOB.Value > now.AddYears(-age)) age--;

                return age.ToString();
            }
        }

        public bool IsValid(ChangeAction action)
        {
            return (GetBusinessRuleViolations(action).Count == 0);
        }

        public void OnValidate(ChangeAction action)
        {
            if (!IsValid(action))
                throw new ApplicationException("User object not in a valid state.");
        }

        public IList<BusinessRuleViolation> GetBusinessRuleViolations(ChangeAction action)
        {
            var violations = new List<BusinessRuleViolation>();

            if (Login.IsNullOrEmpty())
                violations.Add(new BusinessRuleViolation("Login name is required.", "Login"));

            if ((action == ChangeAction.Insert) || (action == ChangeAction.Update))
            {
                if (Current.DB.Query<int>("select 1 from Users where Login = @Login and Id <> @Id", new { Login, Id }).Any())
                {
                    violations.Add(new BusinessRuleViolation("Login name must be unique.", "Login"));
                }
            }

            return violations;
        }
    

        public static User CreateUser(string login, string email, string openIdClaim)
        {
            var u = new User();
            u.CreationDate = DateTime.UtcNow;

            login = CleanLogin(login ?? string.Empty);

            if (login.Length == 0)
            {
                login = emptyLogin;
            }

            u.Login = login;
            u.Email = email;

            int retries = 0;
            bool success = false;

            int maxId = Current.DB.Query<int?>("select max(Id) + 1 from Users").First() ?? 0;
            maxId += 1;

            while (!success)
            {
                IList<BusinessRuleViolation> violations = u.GetBusinessRuleViolations(ChangeAction.Insert);

                if (violations.Any(v => v.PropertyName == "Login"))
                {
                    u.Login = login + (maxId + retries);
                }
                else if (violations.Count > 0)
                {
                    throw new NotImplementedException("The User isn't valid, and we can't compensate for it right now.");
                }
                else
                {
                    success = true;
                }
            }

            u.Id = Current.DB.Users.Insert(new { u.Email, u.Login, u.CreationDate }).Value;
            Current.DB.UserOpenIds.Insert(new { OpenIdClaim = openIdClaim, UserId = u.Id });

            return u;
        }

        private static string CleanLogin(string login)
        {
            login = Regex.Replace(login, "\\s+", ".");
            login = Regex.Replace(login, "[^\\.a-zA-Z0-9]", "");
            return login;
        }

        public static string NormalizeOpenId(string openId, bool normalizeScheme = true)
        {
            openId = (openId ?? "").Trim().ToLowerInvariant();

            if (normalizeScheme && openId.StartsWith("https://"))
                openId = "http://" + openId.Substring(8);

            if (openId.EndsWith("/"))
                openId = openId.Substring(0, openId.Length - 1);

            if (canRemoveWww.IsMatch(openId))
                openId = openId.ReplaceFirst("www.", "");

            return openId;
        }

        // allow varying of "www." only as the third-level domain
        private static Regex canRemoveWww = new Regex(@"^https?://www\.[^./]+\.[^./]+(?:/|$)", RegexOptions.Compiled);

        public string Gravatar(int width)
        {
            return
                String.Format(
                    "<img src=\"http://www.gravatar.com/avatar/{0}?s={1}&amp;d=identicon&amp;r=PG\" height=\"{1}\" width=\"{1}\" class=\"logo\">",
                    Util.GravatarHash(Email ?? Id.ToString()), width + "px"
                    );
        }

        public string UrlTitle
        {
            get { return HtmlUtilities.URLFriendly(Login); }
        }

        public string ProfilePath
        {
            get { return Id + Login.Slugify(); }
        }

        internal static bool MergeUsers(int masterId, int mergeId, StringBuilder log)
        {
            var db = Current.DB;

            string canMergeMsg = "";
            if (!CanMergeUsers(masterId, mergeId, out canMergeMsg))
            {
                log.AppendLine(canMergeMsg);
                return false;
            }

            var masterUser = db.Users.Get(masterId);
            var mergeUser = db.Users.Get(mergeId);

            log.AppendLine(string.Format("Beginning merge of {0} into {1}", mergeId, masterId));

            // Revision Executions
            {
                var updates = db.Execute(@"update r set ExecutionCount = r.ExecutionCount + r2.ExecutionCount
from RevisionExecutions r
join RevisionExecutions r2 on r.SiteId = r2.SiteId and r.RevisionId = r2.RevisionId 
where r.UserId = @masterId and r2.UserId = @mergeId", new { mergeId, masterId });

                var deletes = db.Execute(@"delete r 
from RevisionExecutions r
join RevisionExecutions r2 on r.SiteId = r2.SiteId and r.RevisionId = r2.RevisionId 
where r.UserId = @mergeId  and r2.UserId = @masterId", new { mergeId, masterId });

                var remaps = db.Execute(@"update r set UserId = @masterId
from RevisionExecutions r
where UserId = @mergeId", new { mergeId, masterId });

                log.AppendLine(string.Format("Moving revision executions over {0} dupes, {1} remapped, {2} deleted", updates, remaps, deletes));
            }

            // User Open Ids
            {
                var rempped = db.Execute("update UserOpenIds set UserId = @masterId where UserId = @mergeId", new { mergeId, masterId });
                log.AppendLine(string.Format("Remapped {0} user open ids", rempped));
            }


            // update QuerySets
            {
                var rempped = db.Execute("update QuerySets set OwnerId = @masterId where OwnerId = @mergeId", new { mergeId, masterId });
                log.AppendLine(string.Format("Remapped {0} user query sets", rempped));
            }

            // revisions 
            {
                var rempped = db.Execute("update Revisions set OwnerId = @masterId where OwnerId = @mergeId", new { mergeId, masterId });
                log.AppendLine(string.Format("Remapped {0} revisions", rempped));
            }

            // votes
            {
                var dupes = db.Execute(@"delete v
from Votes v
join Votes v2 on v.VoteTypeId = v2.VoteTypeId and v.QuerySetId = v2.QuerySetId 
where v.UserId = @mergeId and v2.UserId = @masterId", new { mergeId, masterId });

                var rempped = db.Execute("update Votes set UserId = @masterId where UserId = @mergeId", new { mergeId, masterId });

                log.AppendLine(string.Format("Remapped {0} votes, deleted {1} dupes", rempped, dupes));
            }

            // SavedQueries (it is deprecated, but do it just in case)
            try
            {
                var count = db.Execute("update SavedQueries set UserId = @masterId where UserId = @mergeId", new { mergeId, masterId });
                log.AppendLine(string.Format("Remapped {0} saved queries", count));
            }
            catch 
            {
                log.AppendLine("Failed to remap saved queries, perhaps it is time to dump this ... ");
            }

            // User
            log.AppendLine("Moving user properties over");
            string savedLogin = null;
            if (masterUser.Login.StartsWith(emptyLogin) && !mergeUser.Login.StartsWith(emptyLogin)) savedLogin = mergeUser.Login;
            if (masterUser.Email.IsNullOrEmpty() && !mergeUser.Email.IsNullOrEmpty()) masterUser.Email = mergeUser.Email;
            if (!masterUser.IsAdmin && mergeUser.IsAdmin) masterUser.IsAdmin = true;
            if (masterUser.CreationDate.GetValueOrDefault() > mergeUser.CreationDate.GetValueOrDefault()) masterUser.CreationDate = mergeUser.CreationDate;
            if (masterUser.AboutMe.IsNullOrEmpty() && mergeUser.AboutMe.HasValue()) masterUser.AboutMe = mergeUser.AboutMe;
            if (masterUser.Website.IsNullOrEmpty() && mergeUser.Website.HasValue()) masterUser.Website = mergeUser.Website;
            if (masterUser.Location.IsNullOrEmpty() && mergeUser.Location.HasValue()) masterUser.Location = mergeUser.Location;
            if (!masterUser.DOB.HasValue && mergeUser.DOB.HasValue) masterUser.DOB = mergeUser.DOB;
            if (masterUser.LastSeenDate.GetValueOrDefault() < mergeUser.LastSeenDate.GetValueOrDefault())
            {
                masterUser.LastSeenDate = mergeUser.LastSeenDate;
                masterUser.IPAddress = mergeUser.IPAddress;
            }


            var violations = masterUser.GetBusinessRuleViolations(ChangeAction.Update);
            if (violations.Count == 0)
            {
                db.Users.Update(masterUser.Id, new 
                { 
                    masterUser.Email, 
                    masterUser.IsAdmin, 
                    masterUser.CreationDate, 
                    masterUser.AboutMe, 
                    masterUser.Website, 
                    masterUser.Location, 
                    masterUser.DOB, 
                    masterUser.LastSeenDate,
                    masterUser.IPAddress
                });

                log.AppendLine("Updated user record");
            }
            else
            {
                log.AppendLine("**UNABLE TO SUBMIT:");
                violations.ToList().ForEach(v => log.AppendLine(string.Format("--{0}: {1}", v.PropertyName, v.ErrorMessage)));
                return false;
            }


            db.Users.Delete(mergeUser.Id);
            log.AppendLine("Deleted merged user");


            if (savedLogin.HasValue())
            {
                Current.DB.Users.Update(masterUser.Id, new { Login = savedLogin });
                log.AppendLine("Replaced username on master since it was a jon.doe");
            }
            log.AppendLine("That's all folks");
            return true;
        }

        public static bool CanMergeUsers(int masterId, int mergeId, out string canMergeMsg)
        {
            var masterUser = Current.DB.Users.Get(masterId);
            var mergeUser = Current.DB.Users.Get(mergeId); 
            if (masterId == mergeId)
            {
                canMergeMsg = "User ids are identical";
                return false;
            }
            else if (masterUser == null)
            {
                canMergeMsg = string.Format("Master user (id {0}) not found", masterId);
                return false;
            }
            else if (mergeUser == null)
            {
                canMergeMsg = string.Format("Merge user (id {0}) not found", mergeId);
                return false;
            }

            canMergeMsg = null;
            return true;
        }

        int? _savedQueriesCount;
        public int SavedQueriesCount
        {
            get 
            { 
                _savedQueriesCount = _savedQueriesCount ?? Current.DB.Query<int>("SELECT COUNT(*) FROM QuerySets WHERE OwnerId = @userId", new { userId = Id }).FirstOrDefault();
                return _savedQueriesCount.Value;
            }
            set
            {
                _savedQueriesCount = value;
            }
        }

        int? _queryExecutionsCount; 
        public int QueryExecutionsCount
        {
            get 
            { 
                _queryExecutionsCount = _queryExecutionsCount ?? Current.DB.Query<int>("select count(*) from RevisionExecutions where UserId = @userId", new { userId = Id }).FirstOrDefault();
                return _queryExecutionsCount.Value;
            }
            set 
            {
                _queryExecutionsCount = value;
            }
        }
    }
}
