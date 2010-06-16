using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Helpers;
using System.Text.RegularExpressions;
using System.Data.Linq;
using System.Data.SqlClient;

namespace StackExchange.DataExplorer.Models {
    public partial class User {

        /// <summary>
        /// Should be set when ControllerBase determines CurrentUser
        /// </summary>
        public string XSRFFormValue { get; set; }

        public bool IsAnonymous { get; set; }

        public static User CreateUser(string login, string email, string openIdClaim) {
            User u = new User();
            u.CreationDate = DateTime.UtcNow;

            if (login == null) {
                login = "";
            }

            login = CleanLogin(login);

            if (login.Length == 0) {
                if (email != null) {
                    login = CleanLogin(email.Split('@')[0]);
                }

                if (login.Length == 0) {
                    login = "jon.doe";
                }
            }

            u.Login = login;
            u.Email = email;

            int retries = 0; 
            bool success = false;

            while (!success) {
                Current.DB.Users.InsertOnSubmit(u);
                try {
                    Current.DB.SubmitChanges();
                    success = true;
                } catch (Exception e) {
                    var sqlException = e as SqlException;  
                    if (!(
                        e is DuplicateKeyException ||
                        // primary key violation
                        (sqlException != null && sqlException.Number == 0xa43)
                        )) throw;
                    retries++;
                    u.Login = login + retries.ToString();
                }
            }

            UserOpenId o = new UserOpenId();
            o.OpenIdClaim = openIdClaim;
            o.User = u;
            Current.DB.UserOpenIds.InsertOnSubmit(o);
            Current.DB.SubmitChanges();
            return u; 
        }

        private static string CleanLogin(string login) {
            login = Regex.Replace(login, "\\s+", ".");
            login = Regex.Replace(login, "[^\\.a-zA-Z0-9]", "");
            return login;
        }


        public string Gravatar(int width) {

            return String.Format(  "<img src=\"http://www.gravatar.com/avatar/{0}?s={1}&amp;d=identicon&amp;r=PG\" height=\"{1}\" width=\"{1}\" class=\"logo\">",
                Util.GravatarHash(Email ?? Id.ToString()), width.ToString() + "px"
                );
      
        }

        public string SafeAboutMe { get {
            return HtmlUtilities.Safe(HtmlUtilities.RawToCooked(AboutMe ?? ""));
        } }

        public string Age {
            get {
                if (DOB == null) return ""; 

                DateTime now = DateTime.Today;
                int age = now.Year - DOB.Value.Year;
                if (DOB.Value > now.AddYears(-age)) age--;

                return age.ToString();
            }
        }

    }
}