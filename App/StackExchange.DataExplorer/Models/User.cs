using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models
{
    public partial class User
    {
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
                if (email != null)
                    login = CleanLogin(email.Split('@')[0]);

                if (login.Length == 0)
                    login = "jon.doe";
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
    }
}
