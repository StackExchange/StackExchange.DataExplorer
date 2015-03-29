using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using System.Net;

namespace StackExchange.DataExplorer.Controllers
{
    public class CaptchaController : StackOverflowController
    {
        /// <summary>
        /// Handles user captcha submission, either returning user to final destination or the captcha form,
        /// depending on captcha validation success.
        /// </summary>
        [StackRoute("captcha", HttpVerbs.Post)]
        public ActionResult Captcha(FormCollection form)
        {
            var challenge = form.Get<string>("recaptcha_challenge_field", "");

            var validator = new Recaptcha.RecaptchaValidator
            {
                PrivateKey = AppSettings.RecaptchaPrivateKey,
                RemoteIP = GetRemoteIP(),
                Challenge = challenge,
                Response = form.Get<string>("recaptcha_response_field", "")
            };

            Recaptcha.RecaptchaResponse response;

            try
            {
                response = validator.Validate();
            }
            catch (WebException)
            {
                // recaptcha is down - if the challenge had some length (it's usually a massive hash), allow the action - spam will be destroyed by the community
                response = challenge.Length >= 30 ? Recaptcha.RecaptchaResponse.Valid : Recaptcha.RecaptchaResponse.RecaptchaNotReachable;
            }

            if (response == Recaptcha.RecaptchaResponse.Valid)
            {
                Current.SetCachedObjectSliding(CaptchaKey(GetRemoteIP()), true, 60 * 15);
                return Json(new { success = true });
            }
       
            return  Json(new { success = false });;
        }

        private static string CaptchaKey(string ipAddress)
        {
            return "captcha-" + ipAddress;
        }


        public static bool CaptchaPassed(string ipAddress)
        {
            if (AppSettings.RecaptchaPrivateKey.IsNullOrEmpty()) return true;

            return Current.GetCachedObject(CaptchaKey(ipAddress)) != null;
        }
    }
}
