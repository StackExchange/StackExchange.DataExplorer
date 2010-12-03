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
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    [HandleError]
    public class AccountController : StackOverflowController
    {
        private static readonly OpenIdRelyingParty openid = new OpenIdRelyingParty();


        [Route("account/logout")]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return Redirect("~/");
        }

        [Route("account/login", HttpVerbs.Get)]
        public ActionResult Login()
        {
            SetHeader(CurrentUser.IsAnonymous ? "Log in with OpenID" : "Log in below to change your OpenID");

            return View("Login");
        }

        [Route("user/authenticate")]
        [ValidateInput(false)]
        public ActionResult Authenticate(string returnUrl)
        {
            IAuthenticationResponse response = openid.GetResponse();
            if (response == null)
            {
                // Stage 2: user submitting Identifier
                Identifier id;
                if (Identifier.TryParse(Request.Form["openid_identifier"], out id))
                {
                    if (WhiteListEnabled)
                    {
                        var whiteListEntry = Current.DB.OpenIdWhiteLists.FirstOrDefault(w => w.OpenId.ToLower() == id.OriginalString.ToLower());
                        if (whiteListEntry == null || !whiteListEntry.Approved)
                        {
                            if (whiteListEntry == null)
                            {
                                // add a non approved entry to the list
                                whiteListEntry = new OpenIdWhiteList()
                                {
                                    Approved = false,
                                    CreationDate = DateTime.UtcNow,
                                    OpenId = id.OriginalString,
                                    IpAddress = Request.UserHostAddress
                                };

                                Current.DB.OpenIdWhiteLists.InsertOnSubmit(whiteListEntry);
                                Current.DB.SubmitChanges();
                            }

                            // not allowed in 
                            return TextPlain("Not allowed");
                        }
                    }

                    try
                    {
                        IAuthenticationRequest request = openid.CreateRequest(Request.Form["openid_identifier"]);

                        request.AddExtension(new ClaimsRequest
                                                 {
                                                     Email = DemandLevel.Require,
                                                     Nickname = DemandLevel.Request,
                                                     FullName = DemandLevel.Request,
                                                     BirthDate = DemandLevel.Request
                                                 });

                        return request.RedirectingResponse.AsActionResult();
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
                        var sreg = response.GetExtension<ClaimsResponse>();
                        User user = null;

                        UserOpenId openId =
                            Current.DB.UserOpenIds.Where(o => o.OpenIdClaim == response.ClaimedIdentifier.OriginalString)
                                .FirstOrDefault();

                        if (!CurrentUser.IsAnonymous)
                        {
                          if (openId.UserId != CurrentUser.Id) //Does another user have this OpenID
                          {
                            //TODO: Need to perform a user merge
                            ViewData["Message"] = "Another user with this OpenID already exists, merging is not possible at this time.";
                            SetHeader("Log in below to change your OpenID");
                            return View("Login");
                          }
                          openId = CurrentUser.UserOpenIds.FirstOrDefault();
                          openId.OpenIdClaim = response.ClaimedIdentifier.OriginalString;
                          Current.DB.SubmitChanges();
                          user = CurrentUser;
                          returnUrl = "/user/" + user.Id;
                        }
                        else if (openId == null)
                        {
                            // create new user
                            string email = "";
                            string login = "";
                            if (sreg != null)
                            {
                                email = sreg.Email;
                                login = sreg.Nickname;
                            }
                            user = Models.User.CreateUser(login, email, response.ClaimedIdentifier.OriginalString);
                        }
                        else
                        {
                            user = openId.User;
                        }

                        string Groups = user.IsAdmin ? "Admin" : "";

                        var ticket = new FormsAuthenticationTicket(
                            1,
                            user.Id.ToString(),
                            DateTime.Now,
                            DateTime.Now.AddYears(2),
                            true,
                            Groups);

                        string encryptedTicket = FormsAuthentication.Encrypt(ticket);

                        var authenticationCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
                        authenticationCookie.Expires = ticket.Expiration;
                        Response.Cookies.Add(authenticationCookie);


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
    }
}