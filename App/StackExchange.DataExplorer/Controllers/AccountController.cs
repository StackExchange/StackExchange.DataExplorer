using System;
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
        const int StackAppsAuthRetryAttempts = 3;

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
                var user = Models.User.GetByADLogin(username) ?? Models.User.CreateUser(username);
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
                        var request = OpenIdRelay.CreateRequest(id);

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

                        var whitelistEmail = sreg != null && sreg.Email != null && sreg.Email.Length > 2 ? sreg.Email : null;
                        var whiteListResponse = CheckWhitelist(normalizedClaim, whitelistEmail);
                        if (whiteListResponse != null) 
                            return whiteListResponse;

                        User user = null;
                        var openId = Current.DB.Query<UserOpenId>("SELECT * FROM UserOpenIds WHERE OpenIdClaim = @normalizedClaim", new { normalizedClaim }).FirstOrDefault();

                        if (!CurrentUser.IsAnonymous)
                        {
                            if (openId != null && openId.UserId != CurrentUser.Id) //Does another user have this OpenID
                            {
                                //TODO: Need to perform a user merge
                                SetHeader("Log in below to change your OpenID");
                                return LoginError("Another user with this OpenID already exists, merging is not possible at this time.");
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
                                    login = sreg.Nickname ?? sreg.FullName;
                                }
                                user = Models.User.CreateUser(login, email, normalizedClaim);
                            }
                        }
                        else
                        {
                            user = Current.DB.Users.Get(openId.UserId);

                            if (AppSettings.EnableEnforceSecureOpenId && user.EnforceSecureOpenId && !isSecure && openId.IsSecure)
                            {
                                return LoginError("User preferences prohibit insecure (non-https) variants of the provided OpenID identifier");
                            }
                            if (isSecure && !openId.IsSecure)
                            {
                                Current.DB.UserOpenIds.Update(openId.Id, new { IsSecure = true });
                            }
                        }

                        IssueFormsTicket(user);

                        if (!string.IsNullOrEmpty(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        return RedirectToAction("Index", "Home");
                    case AuthenticationStatus.Canceled:
                        return LoginError("Canceled at provider");
                    case AuthenticationStatus.Failed:
                        return LoginError(response.Exception.Message);
                }
            }
            return new EmptyResult();
        }

        private ActionResult LoginViaAccountId(string displayName, int accountId, string returnUrl)
        {
            var syntheticId = Models.User.NormalizeOpenId($"{AppSettings.StackExchangeSyntheticIdPrefix}{accountId}");

            var openId = Current.DB.Query<UserOpenId>(
                @"
                    select UserId
                    from UserOpenIds
                    where OpenIdClaim = @syntheticId",
                new { syntheticId }).FirstOrDefault();

            try
            {
                User user = null;
                if (openId != null)
                {
                    user = Current.DB.Query<User>(
                        @"
                            select *
                            from Users
                            where Id = @UserId"
                        , new { openId.UserId }).FirstOrDefault();
                }
                if (user == null)
                {
                    user = Models.User.CreateUser(displayName, null, syntheticId);
                }

                IssueFormsTicket(user);
                return Redirect(returnUrl);
            }
            catch (Exception ex)
            {
                Current.LogException(ex);
                return ErrorLogin("Error: " + ex.Message, returnUrl);
            }
        }

        private ActionResult LoginViaEmail(string email, string displayName, string returnUrl)
        {
            var whiteListResponse = CheckWhitelist("", email, trustEmail: true);
            if (whiteListResponse != null)
                return whiteListResponse;

            var user = Current.DB.Query<User>("Select * From Users Where Email = @email", new { email }).FirstOrDefault();

            // Create the user if not found
            if (user == null)
            {
                user = Models.User.CreateUser(displayName, email, null);
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

        private ActionResult LoginError(string message) => RedirectToAction("Login", new {message});

        private string BaseUrl => Current.Request.Url.Scheme + "://" + Current.Request.Url.Host;

        private ActionResult OAuthLogin()
        {
            var server = Request.Params["oauth2url"];
            string clientId, secret, path,
                   session = Guid.NewGuid().ToString().Replace("-", "");

            var hash = (AppSettings.OAuthSessionSalt + ":" + session).ToMD5Hash();
            var stateJson = JsonConvert.SerializeObject(new OAuthLoginState { ses = session, hash = hash });

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
            if (server == "https://accounts.google.com/o/oauth2/auth" && TryGetGoogleConfig(out secret, out clientId, out path)) // Google
                    return Redirect(string.Format(
                            "{0}?client_id={1}&scope=openid+email&redirect_uri={2}&state={3}&response_type=code",
                            server,
                            clientId,
                            (BaseUrl + path).UrlEncode(),
                            stateJson.UrlEncode()
                            ));
            else if(server == AppSettings.StackAppsAuthUrl && TryGetStackAppsConfig(out secret, out clientId, out path))
                    return Redirect(string.Format(
                            "{0}?client_id={1}&scope=&redirect_uri={2}&state={3}",
                            server,
                            clientId,
                            (BaseUrl + path).UrlEncode(),
                            stateJson.UrlEncode()
                            ));

            return LoginError("Unsupported OAuth version or server");
        }

        private static bool TryGetGoogleConfig(out string secret , out string clientId, out string path)
        {
            secret = AppSettings.GoogleOAuthSecret;
            clientId = AppSettings.GoogleOAuthClientId;
            path = "/user/oauth/google";
            return AppSettings.EnableGoogleLogin;
        }

        [StackRoute("user/oauth/google")]
        public ActionResult GoogleCallback(string code, string state, string error)
        {
            if (code.IsNullOrEmpty() || error == "access_denied")
            {
                return LoginError("(From Google) Access Denied");
            }
            string secret, clientId, path;
            if (!TryGetGoogleConfig(out secret, out clientId, out path))
            {
                return LoginError("Google Auth not enabled");
            }

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
                    if (authResponse != null)
                    {
                        var loginResponse = FetchFromGoogle(authResponse.access_token);
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

        private ActionResult FetchFromGoogle(string accessToken)
        {
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

                return LoginViaEmail(person.email, person.name, "/");
            }
            catch (Exception e)
            {
                Current.LogException("Error in parsing google response: " + result, e);
                return LoginError("There was an error fetching your account from Google.  Please try logging in again");
            }
        }

        private bool TryGetStackAppsConfig(out string secret , out string clientId, out string path)
        {
            secret = AppSettings.StackAppsOAuthSecret;
            clientId = AppSettings.StackAppsClientId;
            path = "/user/oauth/stackapps";
            return AppSettings.EnableStackAppsAuth;
        }

        [StackRoute("user/oauth/stackapps")]
        public ActionResult StackAppsCallback(string code, string state, string error)
        {
            if (code.IsNullOrEmpty() || error == "access_denied")
            {
                return LoginError("(From StackApps) Access Denied");
            }
            string secret, clientId, path;
            if (!TryGetStackAppsConfig(out secret, out clientId, out path))
            {
                return LoginError("StackApps Auth not enabled");
            }

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

            for (var retry = 0; retry < StackAppsAuthRetryAttempts; retry++)
            {
                StackAppsAuthResponse authResponse;
                try
                {
                    using (var wc = new WebClient())
                    {
                        var response = wc.UploadValues(AppSettings.StackAppsAuthUrl + "/access_token/json", postForm);
                        var responseStr = Encoding.UTF8.GetString(response);
                        authResponse = JsonConvert.DeserializeObject<StackAppsAuthResponse>(responseStr);
                    }
                    if (authResponse != null && authResponse.access_token.HasValue())
                    {
                        var loginResponse = FetchFromStackApps(authResponse.access_token, "/"); // TODO not the actual returnUrl, but LoginViaEmail does the same...
                        if (loginResponse != null) return loginResponse;
                    }
                }
                catch (WebException e)
                {
                    using (var reader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        LogAuthError(new Exception("Error contacting " + AppSettings.StackAppsDomain + ": " + text));
                    }
                    continue;
                }
                catch(Exception e)
                {
                    LogAuthError(e);
                    continue;
                }
            }

            return LoginError("StackApps authentication failed");
        }

        private ActionResult FetchFromStackApps(string accessToken, string returnUrl)
        {
            string result = null;
            Exception lastException = null;
            for (var retry = 0; retry < StackAppsAuthRetryAttempts; retry++)
            {
                try
                {
                    var url = $"https://{AppSettings.StackExchangeApiDomain}/2.2/me?site={HttpUtility.UrlEncode(AppSettings.StackAppsDomain)}&access_token={HttpUtility.UrlEncode(accessToken)}&key={HttpUtility.UrlEncode(AppSettings.StackAppsApiKey)}";
                    var request = WebRequest.CreateHttp(url);
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        result = reader.ReadToEnd();
                        break;
                    }
                }
                catch (WebException e)
                {
                    using (var reader = new StreamReader(e.Response.GetResponseStream()))
                    {
                        var text = reader.ReadToEnd();
                        LogAuthError(new Exception($"Error fetching from {AppSettings.StackExchangeApiDomain}: {text}"));
                    }
                    continue;
                }
                catch (Exception e)
                {
                    lastException = e;
                }
                if (retry == StackAppsAuthRetryAttempts - 1)
                    LogAuthError(lastException);
            }

            if (result.IsNullOrEmpty() || result == "false")
            {
                return LoginError($"Error accessing {AppSettings.StackAppsDomain} user");
            }

            try
            {
                var user = JsonConvert.DeserializeObject<SEApiUserResponse>(result)?.items?.FirstOrDefault();

                if (user == null)
                    return LoginError($"Error fetching user from {AppSettings.StackAppsDomain}");
                if (user.account_id == null)
                    return LoginError($"Error fetching account_id from {AppSettings.StackAppsDomain}");

                return LoginViaAccountId(user.display_name, user.account_id.Value, returnUrl);
            }
            catch (Exception e)
            {
                Current.LogException($"Error in parsing {AppSettings.StackExchangeApiDomain} response: " + result, e);
                return LoginError($"There was an error fetching your account from {AppSettings.StackAppsDomain}. Please try logging in again");
            }
        }

        private void LogAuthError(Exception e)
        {
            if (e == null) return;
            if (e.Message.Contains("Unable to fetch from"))
            {
                //return;
            }

            Current.LogException(e);
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
        public class StackAppsAuthResponse
        {
            public string access_token { get; set; }

            public long expires { get; set; }
        }

        public class SEApiUserResponse
        {
            public SEApiUser[] items { get; set; }
        }

        public class SEApiUser
        {
            public int? account_id { get; set; }
            public int? user_id { get; set; }
            public string link { get; set; }
            public string profile_image { get; set; }
            public string display_name { get; set; }
        }
        // ReSharper restore InconsistentNaming
    }
}