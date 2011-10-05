using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models
{
    public partial class User
    {
        private const string emptyLogin = "jon.doe";

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

        partial void OnValidate(ChangeAction action)
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
                if (Current.DB.Users.Any<User>(u => (u.Login == Login) && (u.Id != Id)))
                    violations.Add(new BusinessRuleViolation("Login name must be unique.", "Login"));
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
                /* email scrubbing got people upset, so it is gone now
                if (email != null)
                    login = CleanLogin(email.Split('@')[0]);
                 */

                if (login.Length == 0)
                    login = emptyLogin;
            }

            u.Login = login;
            u.Email = email;

            int retries = 0;
            bool success = false;

            int maxId = Current.DB.ExecuteQuery<int?>("select max(Id) + 1 from Users").First() ?? 0;
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

            Current.DB.Users.InsertOnSubmit(u);

            var o = new UserOpenId();
            o.OpenIdClaim = openIdClaim;
            o.User = u;

            Current.DB.UserOpenIds.InsertOnSubmit(o);
            Current.DB.SubmitChanges();
            return u;
        }

        private static string CleanLogin(string login)
        {
            login = Regex.Replace(login, "\\s+", ".");
            login = Regex.Replace(login, "[^\\.a-zA-Z0-9]", "");
            return login;
        }

        public static string NormalizeOpenId(string openId)
        {
            openId = (openId ?? "").Trim().ToLowerInvariant();

            if (openId.StartsWith("https://"))
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

        internal static bool MergeUsers(int masterId, int mergeId, StringBuilder log)
        {
            var db = Current.DB;

            string canMergeMsg = "";
            if (!CanMergeUsers(masterId, mergeId, out canMergeMsg))
            {
                log.AppendLine(canMergeMsg);
                return false;
            }

            var masterUser = db.Users.Where(u => u.Id == masterId).First();
            var mergeUser = db.Users.Where(u => u.Id == mergeId).First();

            log.AppendLine(string.Format("Beginning merge of {0} into {1}", mergeId, masterId));

            // Queries
            var queries = db.Queries.Where(q => q.CreatorId == mergeId).ToList();
            log.AppendLine(string.Format("Moving {0} queries over", queries.Count));
            queries.ForEach(q => q.CreatorId = masterId);

            // Query Executions
            var queryExecutions = db.QueryExecutions.Where(qe => qe.UserId == mergeId).ToList();
            log.AppendLine(string.Format("Moving {0} query executions over", queryExecutions.Count));
            queryExecutions.ForEach(qe => qe.UserId = masterId);

            // Saved Queries
            var savedQueries = db.SavedQueries.Where(sq => sq.UserId == mergeId).ToList();
            log.AppendLine(string.Format("Moving {0} saved queries over", savedQueries.Count));
            savedQueries.ForEach(sq => sq.UserId = masterId);

            // User Open Ids
            var userOpenIds = db.UserOpenIds.Where(uoi => uoi.UserId == masterId).Skip(1).ToList();
            log.AppendLine(string.Format("Removing {0} inaccessible openids found for master", userOpenIds.Count));
            userOpenIds.ForEach(uoi => log.AppendLine(string.Format("--Dropping {0} as an open id for the master user", uoi.OpenIdClaim)));
            db.UserOpenIds.DeleteAllOnSubmit(userOpenIds);

            userOpenIds = db.UserOpenIds.Where(uoi => uoi.UserId == mergeId).ToList();
            log.AppendLine(string.Format("Removing {0} openids for mergee", userOpenIds.Count));
            userOpenIds.ForEach(uoi => log.AppendLine(string.Format("--Dropping {0} as an open id for the mergee", uoi.OpenIdClaim)));
            db.UserOpenIds.DeleteAllOnSubmit(userOpenIds);

            // User
            log.AppendLine("Moving user properties over");
            if (masterUser.Login.StartsWith(emptyLogin) && !mergeUser.Login.StartsWith(emptyLogin)) masterUser.Login = mergeUser.Login;
            if (masterUser.Email.IsNullOrEmpty() && !mergeUser.Email.IsNullOrEmpty()) masterUser.Email = mergeUser.Email;
            // if (masterUser.LastLogin.GetValueOrDefault() < mergeUser.LastLogin.GetValueOrDefault()) masterUser.LastLogin = mergeUser.LastLogin;
            if (!masterUser.IsAdmin && mergeUser.IsAdmin) masterUser.IsAdmin = true;
            if (!masterUser.IsModerator && mergeUser.IsModerator) masterUser.IsModerator = true;
            if (masterUser.CreationDate.GetValueOrDefault() > mergeUser.CreationDate.GetValueOrDefault()) masterUser.CreationDate = mergeUser.CreationDate;
            if (masterUser.AboutMe.IsNullOrEmpty() && mergeUser.AboutMe.HasValue()) masterUser.AboutMe = mergeUser.AboutMe;
            if (masterUser.Website.IsNullOrEmpty() && mergeUser.Website.HasValue()) masterUser.Website = mergeUser.Website;
            if (masterUser.Location.IsNullOrEmpty() && mergeUser.Location.HasValue()) masterUser.Location = mergeUser.Location;
            if (!masterUser.DOB.HasValue && mergeUser.DOB.HasValue) masterUser.DOB = mergeUser.DOB;
            // if (masterUser.LastActivityDate.GetValueOrDefault() < mergeUser.LastActivityDate.GetValueOrDefault()) masterUser.LastActivityDate = mergeUser.LastActivityDate;
            if (masterUser.LastSeenDate.GetValueOrDefault() < mergeUser.LastSeenDate.GetValueOrDefault())
            {
                masterUser.LastSeenDate = mergeUser.LastSeenDate;
                masterUser.IPAddress = mergeUser.IPAddress;
            }
            
            // Votes
            var mergeVotes = db.Votes.Where(v => v.UserId == mergeId).ToList();
            var masterVotes = db.Votes.Where(v => v.UserId == masterId).ToList();
            int dupe = 0;
            mergeVotes.ForEach(mergeVote =>
            {
                if (masterVotes.Exists(masterVote => masterVote.SavedQueryId == mergeVote.SavedQueryId))
                {
                    db.Votes.DeleteOnSubmit(mergeVote);
                    dupe++;
                }
                else mergeVote.UserId = masterId;
            });
            log.AppendLine(string.Format("Removed {0} dupe votes", dupe));

            log.AppendLine("Deleting merged user");
            db.Users.DeleteOnSubmit(mergeUser);
            db.SubmitChanges();
            log.AppendLine("That's all folks");
            return true;
        }

        public static bool CanMergeUsers(int masterId, int mergeId, out string canMergeMsg)
        {
            var masterUser = Current.DB.Query<User>("select Id from Users where Id = @Id", new { Id = masterId }).SingleOrDefault();
            var mergeUser = Current.DB.Query<User>("select Id from Users where Id = @Id", new { Id = mergeId }).SingleOrDefault();
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

        public int SavedQueriesCount
        {
            get { return Current.DB.Query<int>("select count(*) from SavedQueries where UserId = @userId", new { userId = Id }).FirstOrDefault(); }
        }

        public int QueryExecutionsCount
        {
            get { return Current.DB.Query<int>("select count(*) from QueryExecutions where UserId = @userId", new { userId = Id }).FirstOrDefault(); }
        }
    }
}
