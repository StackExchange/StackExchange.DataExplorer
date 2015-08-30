using System;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
using Newtonsoft.Json;

namespace StackExchange.DataExplorer.Controllers
{
    [HandleError]
    public class AccountController : StackOverflowController
    {
        private static readonly OpenIdRelyingParty OpenIdRelay = new OpenIdRelyingParty();
        const int GoogleAuthRetryAttempts = 3;

        [StackRoute("account/logout")]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return Redirect("~/");
        }

        [StackRoute("account/login", HttpVerbs.Get)]
        public ActionResult Login(string returnUrl, string message = null)
        {
            ViewData["Message"] = message;
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
                    return ErrorLogin("User is now in allowed Active Directory groups.", returnUrl);
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
            IAuthenticationResponse response = OpenIdRelay.GetResponse();
            if (response == null)
            {
                if (Request.Params[Keys.OAuth2Url].HasValue())
                {
                    // Push the form and mark it valid (nothing to confirm, anything that comes down via OAuth is clean right now
                    return OAuthLogin();
                }

                // Stage 2: user submitting Identifier
                Identifier id;

                if (Identifier.TryParse(Request.Form[Keys.OpenId], out id))
                {
                    try
                    {
                        IAuthenticationRequest request = OpenIdRelay.CreateRequest(id);

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
                        return LoginError(ex.Message);
                    }
                }

                return LoginError("Invalid identifier");
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
                        var email = sreg != null && sreg.Email != null && sreg.Email.Length > 2 ? sreg.Email : null;
                        var displayName = sreg != null ? sreg.Nickname ?? sreg.FullName : null;

                        return LoginUser(new UserAuthClaim.Identifier(normalizedClaim, UserAuthClaim.ClaimType.OpenID),  email, displayName,
                            returnUrl, IsVerifiedEmailProvider(originalClaim), isSecure);
                    case AuthenticationStatus.Canceled:
                        return LoginError("Canceled at provider");
                    case AuthenticationStatus.Failed:
                        return LoginError(response.Exception.Message);
                }
            }
            return new EmptyResult();
        }

        private ActionResult LoginUser(UserAuthClaim.Identifier identifier, string email, string displayName, string returnUrl, bool trustEmail, bool isSecure = false, UserAuthClaim.Identifier legacyIdentifier = null)
        {
            var whiteListResponse = CheckWhitelist(identifier.Value, email, trustEmail: trustEmail);

            if (whiteListResponse != null)
                return whiteListResponse;

            Tuple<User, UserAuthClaim> identity = Models.User.FindUserIdentityByAuthClaim(email, identifier, CurrentUser.IsAnonymous && trustEmail, legacyIdentifier: legacyIdentifier);

            var user = identity.Item1;
            var claim = identity.Item2;

            if (!CurrentUser.IsAnonymous && user != null && user.Id != CurrentUser.Id)
            {
                //TODO: Need to perform a user merge
                SetHeader("Log in below to change your OpenID");

                return LoginError("Another user with this login already exists, merging is not possible at this time.");
            }

            if (user == null)
            {
                user = !CurrentUser.IsAnonymous ? CurrentUser : Models.User.CreateUser(displayName, email);
            }

            if (claim == null)
            {
                Current.DB.UserAuthClaims.Insert(new { UserId = user.Id, ClaimIdentifier = identifier.Value, IdentifierType = identifier.Type, IsSecure = isSecure, Display = trustEmail ? email : null });
            }
            else if (claim.UserId != user.Id)
            {
                // This implies there's an orphan claim record somehow, I don't think this should be possible
                Current.DB.UserAuthClaims.Update(claim.Id, new { UserId = user.Id });
            }
            else
            {
                // This checking is only relevant to OpenID…which is kind of auth-type specific logic for this method, but meh
                if (AppSettings.EnableEnforceSecureOpenId && user.EnforceSecureOpenId && !isSecure && claim.IsSecure)
                {
                    return LoginError("User preferences prohibit insecure (non-https) variants of the provided OpenID identifier");
                }
                else if (isSecure && !claim.IsSecure)
                {
                    Current.DB.UserAuthClaims.Update(claim.Id, new { IsSecure = true });
                }

                if (trustEmail && claim.Display != email)
                {
                    Current.DB.UserAuthClaims.Update(claim.Id, new { Display = email });
                }
            }

            IssueFormsTicket(user);

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private ActionResult CheckWhitelist(string normalizedClaim, string email = null, bool trustEmail = false)
        {
            if (!AppSettings.EnableWhiteList) return null;

            // Ideally we'd be as strict as possible here and use originalClaim, but we might break existing deployments then
            string lookupClaim = normalizedClaim;
            bool attemptUpdate = false;

            if ((trustEmail || IsVerifiedEmailProvider(normalizedClaim)) && email != null)
            {
                attemptUpdate = true;
                lookupClaim = "email:" + email;
            }

            var whiteListEntry = Current.DB.Query<OpenIdWhiteList>("SELECT * FROM OpenIdWhiteList WHERE lower(OpenId) = @lookupClaim", new { lookupClaim }).FirstOrDefault();
            if (whiteListEntry == null && attemptUpdate)
            {
                whiteListEntry = Current.DB.Query<OpenIdWhiteList>("SELECT * FROM OpenIdWhiteList WHERE LOWER(OpenId) = @normalizedClaim", new { normalizedClaim }).FirstOrDefault();
                if (whiteListEntry != null)
                {
                    whiteListEntry.OpenId = lookupClaim;
                    Current.DB.OpenIdWhiteList.Update(whiteListEntry.Id, new { whiteListEntry.OpenId });
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

            return null;
        }

        private ActionResult LoginError(string message)
        {
            return RedirectToAction("Login", new {message});
        }

        private string BaseUrl
        {
            get { return Current.Request.Url.Scheme + "://" + Current.Request.Url.Host; }
        }

        private ActionResult OAuthLogin()
        {
            var server = Request.Params["oauth2url"];
            string clientId, secret, path,
                   session = Guid.NewGuid().ToString().Replace("-", "");

            var hash = (AppSettings.OAuthSessionSalt + ":" + session).ToMD5Hash();
            var stateJson = JsonConvert.SerializeObject(new OAuthLoginState { ses = session, hash = hash });

            switch (server)
            {
                //case "https://graph.facebook.com/oauth/authorize": // Facebook
                //    GetFacebookConfig(out secret, out clientId);
                //    var redirect = string.Format(
                //        "{0}?client_id={1}&scope=email&redirect_uri={2}/{3}&state={4}",
                //        server,
                //        clientId,
                //        host,
                //        session,
                //        state);
                //    return Redirect(redirect);
                case "https://accounts.google.com/o/oauth2/auth": // Google
                    GetGoogleConfig(out secret, out clientId, out path);

                    return Redirect(string.Format(
                        "{0}?client_id={1}&scope=openid+email&redirect_uri={2}&state={3}&response_type=code&openid.realm={2}",
                        server,
                        clientId,
                        (BaseUrl + path).UrlEncode(),
                        stateJson.UrlEncode()
                    ));
            }

            return LoginError("Unsupported OAuth version or server");
        }

        private void GetGoogleConfig(out string secret , out string clientId, out string path)
        {
            secret = AppSettings.GoogleOAuthSecret;
            clientId = AppSettings.GoogleOAuthClientId;
            path = "/user/oauth/google";
        }

        [StackRoute("user/oauth/google")]
        public ActionResult GoogleCallback(string code, string state, string error)
        {
            if (code.IsNullOrEmpty() || error == "access_denied")
            {
                return LoginError("(From Google) Access Denied");
            }
            string secret, clientId, path;
            GetGoogleConfig(out secret, out clientId, out path);

            // Verify state
            var oAuthState = JsonConvert.DeserializeObject<OAuthLoginState>(state);
            var hash = (AppSettings.OAuthSessionSalt + ":" + oAuthState.ses).ToMD5Hash();
            if (oAuthState.hash != hash)
            {
                return LoginError("Invalid verification hash");
            }

            var postForm = HttpUtility.ParseQueryString("");
            postForm["code"] = code;
            postForm["client_id"] = clientId;
            postForm["client_secret"] = secret;
            postForm["redirect_uri"] = BaseUrl + path;
            postForm["grant_type"] = "authorization_code";

            for (var retry = 0; retry < GoogleAuthRetryAttempts; retry++)
            {
                GoogleAuthResponse authResponse;
                try
                {
                    using (var wc = new WebClient())
                    {
                        var response = wc.UploadValues("https://accounts.google.com/o/oauth2/token", postForm);
                        var responseStr = Encoding.UTF8.GetString(response);
                        authResponse = JsonConvert.DeserializeObject<GoogleAuthResponse>(responseStr);
                    }

                    if (authResponse != null && !authResponse.error.HasValue())
                    {
                        var loginResponse = FetchFromGoogle(authResponse.access_token, authResponse.id_token);
                        if (loginResponse != null) return loginResponse;
                    }
                }
                catch (WebException e)
                {
                    using (var reader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        LogAuthError(new Exception("Error contacting google: " + text));
                    }
                    continue;
                }
                catch(Exception e)
                {
                    LogAuthError(e);
                    continue;
                }
                if (authResponse != null && authResponse.error.HasValue())
                {
                    return LoginError(authResponse.error + " " + authResponse.error_description);
                }

                if (retry == GoogleAuthRetryAttempts - 1)
                {
                    if (authResponse != null)
                        LogAuthError(new Exception(authResponse.error + ": " + authResponse.error_description));
                }
            }

            return LoginError("Google authentication failed");
        }

        private ActionResult FetchFromGoogle(string accessToken, string idToken)
        {
            // We're not bothering to validate the id token because we just got it back directly from Google over HTTPS
            var legacyIdentifier = (string)new JwtSecurityToken(idToken).Payload["openid_id"];

            string result = null;
            Exception lastException = null;
            for (var retry = 0; retry < GoogleAuthRetryAttempts; retry++)
            {
                try
                {
                    var url = "https://www.googleapis.com/oauth2/v1/userinfo?access_token=" + accessToken;
                    using (var wc = new WebClient())
                    {
                        result = wc.DownloadString(url);
                        if (result.HasValue())
                        {
                            break;
                        }
                    }
                }
                catch (WebException e)
                {
                    using (var reader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        LogAuthError(new Exception("Error fetching from google: " + text));
                    }
                    continue;
                }
                catch (Exception e)
                {
                    lastException = e;
                }
                if (retry == GoogleAuthRetryAttempts - 1)
                    LogAuthError(lastException);
            }

            if (result.IsNullOrEmpty() || result == "false")
            {
                return LoginError("Error accessing Google account");
            }

            try
            {
                var person = JsonConvert.DeserializeObject<GooglePerson>(result);

                if (person == null)
                    return LoginError("Error fetching user from Google");
                if (person.email == null)
                    return LoginError("Error fetching email from Google");

                return LoginUser(
                    new UserAuthClaim.Identifier(person.id, UserAuthClaim.ClaimType.Google),
                    person.email,
                    person.name,
                    "/",
                    person.verified_email,
                    legacyIdentifier: legacyIdentifier.HasValue() ? new UserAuthClaim.Identifier(Models.User.NormalizeOpenId(legacyIdentifier), UserAuthClaim.ClaimType.OpenID) : null
                );
            }
            catch (Exception e)
            {
                GlobalApplication.LogException(new Exception("Error in parsing google response: " + result, e));
                return LoginError("There was an error fetching your account from Google.  Please try logging in again");
            }
        }

        private void LogAuthError(Exception e)
        {
            if (e == null) return;
            if (e.Message.Contains("Unable to fetch from"))
            {
                //return;
            }

            GlobalApplication.LogException(e);
        }

        private void IssueFormsTicket(User user)
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

        // ReSharper disable InconsistentNaming

        public class OAuthLoginState
        {
            public string ses { get; set; }
            public int sid { get; set; }
            public string hash { get; set; }
        }

        public class GoogleAuthResponse
        {
            public string access_token { get; set; }
            public string id_token { get; set; }
            public string error { get; set; }
            public string error_description { get; set; }
        }
        public class GooglePerson
        {
            // Example response:
            //{
            //    "id": "00000000000000",
            //    "email": "fred.example@gmail.com",
            //    "verified_email": true,
            //    "name": "Fred Example",
            //    "given_name": "Fred",
            //    "family_name": "Example",
            //    "picture": "https://lh5.googleusercontent.com/-2Sv-4bBMLLA/AAAAAAAAAAI/AAAAAAAAABo/bEG4kI2mG0I/photo.jpg",
            //    "gender": "male",
            //    "locale": "en-US"
            //}
            public string id { get; set; }
            public string email { get; set; }
            public bool verified_email { get; set; }
            public string name { get; set; }
            public string given_name { get; set; }
            public string family_name { get; set; }
            public string picture { get; set; }
            public string gender { get; set; }
            public string locale { get; set; }
        }

        // ReSharper restore InconsistentNaming
    }
}