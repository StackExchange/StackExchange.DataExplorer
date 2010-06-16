using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using StackExchange.DataExplorer.Models;
using DotNetOpenAuth.OpenId.RelyingParty;
using DotNetOpenAuth.OpenId;
using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OpenId.Extensions.SimpleRegistration;
using DotNetOpenAuth.OpenId.Extensions.AttributeExchange;

namespace StackExchange.DataExplorer.Controllers {

    [HandleError]
    public class AccountController : StackOverflowController {

        private static OpenIdRelyingParty openid = new OpenIdRelyingParty();

		public ActionResult Index() {
			if (!User.Identity.IsAuthenticated) {
				Response.Redirect("~/account/login?ReturnUrl=Index");
			}

			return View("Index");
		}

        [Helpers.Route("account/logout")]
		public ActionResult Logout() {
			FormsAuthentication.SignOut();
			return Redirect("~/");
		}

        [Helpers.Route("account/login", HttpVerbs.Get)]
		public ActionResult Login() {

            SetHeader("Log in with OpenID");

			return View("Login");
		}

        [Helpers.Route("user/authenticate")]
		[ValidateInput(false)]
		public ActionResult Authenticate(string returnUrl) {
			var response = openid.GetResponse();
			if (response == null) {
				// Stage 2: user submitting Identifier
				Identifier id;
				if (Identifier.TryParse(Request.Form["openid_identifier"], out id)) {
					try {
						var request = openid.CreateRequest(Request.Form["openid_identifier"]);

                        request.AddExtension(new ClaimsRequest {
                            Email = DemandLevel.Require,
                            Nickname = DemandLevel.Request, 
                            FullName = DemandLevel.Request,
                            BirthDate = DemandLevel.Request
                        });

                        return request.RedirectingResponse.AsActionResult();
					} catch (ProtocolException ex) {
						ViewData["Message"] = ex.Message;
						return View("Login");
					}
				} else {
					ViewData["Message"] = "Invalid identifier";
					return View("Login");
				}
			} else {
				// Stage 3: OpenID Provider sending assertion response
				switch (response.Status) {
					case AuthenticationStatus.Authenticated:
                        var sreg = response.GetExtension<ClaimsResponse>();
                        User user = null;

                        var openId = Current.DB.UserOpenIds.Where(o => o.OpenIdClaim == response.ClaimedIdentifier.OriginalString).FirstOrDefault();
                        if (openId == null) {
                            // create new user
                            string email = "";
                            string login = "";
                            if (sreg != null) {
                                email = sreg.Email;
                                login = sreg.Nickname;
                            }
                            user = Models.User.CreateUser(login, email, response.ClaimedIdentifier.OriginalString);

                        } else {
                            user = openId.User;
                        }

                        string Groups = user.IsAdmin ? "Admin" : "";

                        FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                          1, 
                          user.Id.ToString(), 
                          DateTime.Now, 
                          DateTime.Now.AddYears(2), 
                          true,
                          Groups); 
 
                        string encryptedTicket = FormsAuthentication.Encrypt(ticket);
 
                        HttpCookie authenticationCookie = new HttpCookie(FormsAuthentication.FormsCookieName,encryptedTicket);
                        authenticationCookie.Expires = ticket.Expiration;
                        Response.Cookies.Add(authenticationCookie); 

                        
						if (!string.IsNullOrEmpty(returnUrl)) {
							return Redirect(returnUrl);
						} else {
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
