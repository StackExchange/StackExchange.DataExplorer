using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OpenId;
using DotNetOpenAuth.OpenId.Extensions.SimpleRegistration;
using DotNetOpenAuth.OpenId.RelyingParty;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Helpers.Security;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    [HandleError]
    public class AccountController : StackOverflowController
    {
        private static readonly OpenIdRelyingParty openid = new OpenIdRelyingParty();

        [StackRoute("account/logout")]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return Redirect("~/");
        }

        [StackRoute("account/login", HttpVerbs.Get)]
        public ActionResult Login(string returnUrl)
        {
            switch (AppSettings.AuthMethod)
            {
                case AppSettings.AuthenitcationMethod.ActiveDirectory:
                    SetHeader("Log in with Active Directory");
                    return View("LoginActiveDirectory");
                //case AppSettings.AuthenitcationMethod.Default:
                default:
                    SetHeader(CurrentUser.IsAnonymous ? "Log in with OpenID" : "Log in below to change your OpenID");
                    return View("Login");
            }
        }

        [StackRoute("account/ad-login", HttpVerbs.Post), ValidateInput(false)]
        public ActionResult LoginActiveDirectory(string returnUrl)
        {
            string username = Request.Form["username"].IsNullOrEmptyReturn("").Trim(),
                   password = Request.Form["password"].IsNullOrEmptyReturn("").Trim();

            // TODO: Sanitize username
            try
            {
                if (!ActiveDirectory.AuthenticateUser(username, password))
                {
                    return ErrorLogin("Authentication failed.", returnUrl);
                }
                if (!ActiveDirectory.IsUser(username))
                {
                    return ErrorLogin("User is not in allowed Active Directory groups.", returnUrl);
                }
                var user = Models.User.GetByADLogin(username);
                if (user == null)
                {
                    user = Models.User.CreateUser(username);
                }
                if (user == null)
                {
                    return ErrorLogin("Error creating user for " + username + ".", returnUrl);
                }
                var isAdmin = ActiveDirectory.IsAdmin(username);
                user.SetAdmin(isAdmin); // Intentionally refresh admin status on login
                ActiveDirectory.SetProperties(user); // TODO: Optimize this down to a single PricipalContext open/close

                IssueFormsTicket(user);
            }
            catch (Exception ex)
            {
                Current.LogException(ex);
                return ErrorLogin("Error: " + ex.Message, returnUrl);
            }

            return Redirect(returnUrl);
        }

        public ActionResult ErrorLogin(string message, string returnUrl)
        {
            ViewData["Message"] = message;
            return Login(returnUrl);
        }

        [StackRoute("user/authenticate"), ValidateInput(false)]
        public ActionResult Authenticate(string returnUrl)
        {
            IAuthenticationResponse response = openid.GetResponse();
            if (response == null)
            {
                // Stage 2: user submitting Identifier
                Identifier id;

                if (Identifier.TryParse(Request.Form["openid_identifier"], out id))
                {
                    try
                    {
                        IAuthenticationRequest request = openid.CreateRequest(id);

                        request.AddExtension(
                            new ClaimsRequest
                            {
                                Email = DemandLevel.Require,
                                Nickname = DemandLevel.Request,
                                FullName = DemandLevel.Request,
                                BirthDate = DemandLevel.Request
                            }
                        );

                        return request.RedirectingResponse.AsActionResultMvc5();
                    }
                    catch (ProtocolException ex)
                    {
                        ViewData["Message"] = ex.Message;
                        return View("Login");
                    }
                }
                else
                {
                    ViewData["Message"] = "Invalid identifier";
                    return View("Login");
                }
            }
            else
            {
                // Stage 3: OpenID Provider sending assertion response
                switch (response.Status)
                {
                    case AuthenticationStatus.Authenticated:
                        var originalClaim = Models.User.NormalizeOpenId(response.ClaimedIdentifier.ToString(), false);
                        var normalizedClaim = Models.User.NormalizeOpenId(response.ClaimedIdentifier.ToString());
                        var sreg = response.GetExtension<ClaimsResponse>();
                        var isSecure = originalClaim.StartsWith("https://");

                        if (AppSettings.EnableWhiteList)
                        {
                            // Ideally we'd be as strict as possible here and use originalClaim, but we might break existing deployments then
                            string lookupClaim = normalizedClaim;
                            bool attemptUpdate = false;

                            if (IsVerifiedEmailProvider(normalizedClaim) && sreg.Email != null && sreg.Email.Length > 2)
                            {
                                attemptUpdate = true;
                                lookupClaim = "email:" + sreg.Email;
                            }

                            var whiteListEntry = Current.DB.Query<OpenIdWhiteList>("select * from OpenIdWhiteList where lower(OpenId) = @lookupClaim", new { lookupClaim }).FirstOrDefault();

                            if (whiteListEntry == null && attemptUpdate)
                            {
                                whiteListEntry = Current.DB.Query<OpenIdWhiteList>("SELECT * FROM OpenIdWhiteList WHERE LOWER(OpenId) = @normalizedClaim", new { normalizedClaim }).FirstOrDefault();

                                if (whiteListEntry != null)
                                {
                                    whiteListEntry.OpenId = lookupClaim;

                                    Current.DB.OpenIdWhiteList.Update(whiteListEntry.Id, new { OpenId = whiteListEntry.OpenId });
                                }
                            }
                            
                            if (whiteListEntry == null || !whiteListEntry.Approved)
                            {
                                if (whiteListEntry == null)
                                {
                                    // add a non approved entry to the list
                                    var newEntry = new 
                                    {
                                        Approved = false,
                                        CreationDate = DateTime.UtcNow,
                                        OpenId = lookupClaim,
                                        IpAddress = Request.UserHostAddress
                                    };

                                    Current.DB.OpenIdWhiteList.Insert(newEntry);
 
                                }

                                // not allowed in 
                                return TextPlain("Not allowed");
                            }
                        }

                        User user = null;
                        var openId = Current.DB.Query<UserOpenId>("SELECT * FROM UserOpenIds WHERE OpenIdClaim = @normalizedClaim", new { normalizedClaim }).FirstOrDefault();

                        if (!CurrentUser.IsAnonymous)
                        {
                            if (openId != null && openId.UserId != CurrentUser.Id) //Does another user have this OpenID
                            {
                                //TODO: Need to perform a user merge
                                ViewData["Message"] = "Another user with this OpenID already exists, merging is not possible at this time.";
                                SetHeader("Log in below to change your OpenID");
                                return View("Login");
                            }

                            var currentOpenIds = Current.DB.Query<UserOpenId>("select * from UserOpenIds  where UserId = @Id", new {CurrentUser.Id});

                            // If a user is merged and then tries to add one of the OpenIDs used for the two original users,
                            // this update will fail...so don't attempt it if we detect that's the case. Really we should
                            // work on allowing multiple OpenID logins, but for now I'll settle for not throwing an exception...
                            if (!currentOpenIds.Any(s => s.OpenIdClaim == normalizedClaim))
                            {
                                Current.DB.UserOpenIds.Update(currentOpenIds.First().Id, new { OpenIdClaim = normalizedClaim });
                            }
                          
                            user = CurrentUser;
                            returnUrl = "/users/" + user.Id;
                        }
                        else if (openId == null)
                        {

                            if (sreg != null && IsVerifiedEmailProvider(normalizedClaim))
                            {
                                // Eh...We can trust the verified email provider, but we can't really trust Users.Email.
                                // I can't think of a particularly malicious way this could be exploited, but it's likely
                                // worth reviewing at some point.
                                user = Current.DB.Query<User>("select * from Users where Email = @Email", new { sreg.Email }).FirstOrDefault();

                                if (user != null)
                                {
                                    Current.DB.UserOpenIds.Insert(new { UserId = user.Id, OpenIdClaim = normalizedClaim, isSecure });
                                }
                            }

                            if (user == null)
                            {
                                // create new user
                                string email = "";
                                string login = "";
                                if (sreg != null)
                                {
                                    email = sreg.Email;
                                    login = sreg.Nickname;
                                }
                                user = Models.User.CreateUser(login, email, normalizedClaim);
                            }
                        }
                        else
                        {
                            user = Current.DB.Users.Get(openId.UserId);

                            if (AppSettings.EnableEnforceSecureOpenId && user.EnforceSecureOpenId && !isSecure && openId.IsSecure)
                            {
                                ViewData["Message"] = "User preferences prohibit insecure (non-https) variants of the provided OpenID identifier";
                                return View("Login");
                            }
                            else if (isSecure && !openId.IsSecure)
                            {
                                Current.DB.UserOpenIds.Update(openId.Id, new { IsSecure = true });
                            }
                        }

                        IssueFormsTicket(user);

                        if (!string.IsNullOrEmpty(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    case AuthenticationStatus.Canceled:
                        ViewData["Message"] = "Canceled at provider";
                        return View("Login");
                    case AuthenticationStatus.Failed:
                        ViewData["Message"] = response.Exception.Message;
                        return View("Login");
                }
            }
            return new EmptyResult();
        }

        private void IssueFormsTicket(Models.User user)
        {
            var ticket = new FormsAuthenticationTicket(
                1,
                user.Id.ToString(),
                DateTime.Now,
                DateTime.Now.AddYears(2),
                true,
                "");

            string encryptedTicket = FormsAuthentication.Encrypt(ticket);

            var authenticationCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                Expires = ticket.Expiration,
                HttpOnly = true,
                Secure = Current.IsSecureConnection
            };
            Response.Cookies.Add(authenticationCookie);
        }

        private bool IsVerifiedEmailProvider(string identifier)
        {
            identifier = identifier.ToLowerInvariant();

            if (identifier.Contains("@")) return false;
            if (identifier.StartsWith(@"https://www.google.com/accounts/o8/id")) return true;
            if (identifier.StartsWith(@"https://me.yahoo.com/")) return true;
            if (identifier.StartsWith(@"http://stackauth.com/")) return true;
            if (identifier.StartsWith(@"https://openid.stackexchange.com/")) return true;
            if (identifier.StartsWith(@"https://plus.google.com/")) return true;

            return false;
        }
    }
}