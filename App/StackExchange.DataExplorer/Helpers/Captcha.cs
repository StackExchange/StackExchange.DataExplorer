using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using Newtonsoft.Json;

namespace StackExchange.DataExplorer.Helpers
{
    public static class Captcha
    {
        public static bool ChallengeIsvalid(NameValueCollection form)
        {
            if (IsPassedForCurrentUser())
            {
                return true;
            }

            var challenge = form.Get("g-recaptcha-response", "");
            bool userIsValidated = false;

            if (challenge.HasValue())
            {
                using (var client = new HttpClient())
                {
                    var response = client.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(new[] {
                        new KeyValuePair<string, string>("secret", AppSettings.RecaptchaPrivateKey),
                        new KeyValuePair<string, string>("response", challenge),
                        new KeyValuePair<string, string>("remoteip", Current.RemoteIP)
                    })).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        using (var content = response.Content)
                        {
                            userIsValidated = ((CaptchaResponse)JsonConvert.DeserializeObject(content.ReadAsStringAsync().Result, typeof(CaptchaResponse))).Success;
                        }
                    }
                    else
                    {
                        // recaptcha is down - if the challenge had some length (it's usually a massive hash), allow the action - spam will be destroyed by the community
                        userIsValidated = challenge.Length >= 30;
                    }
                }

                if (userIsValidated)
                {
                    Current.SetCachedObjectSliding(CaptchaKey(Current.RemoteIP), true, 60 * 30);
                }
            }

            return userIsValidated;
        }

        public static bool IsShownForCurrentUser()
        {
            return AppSettings.RecaptchaPrivateKey.HasValue() && Current.User.IsAnonymous;
        }

        public static bool IsPassedForCurrentUser()
        {
            return !IsShownForCurrentUser() || Current.GetCachedObject(CaptchaKey(Current.RemoteIP)) != null;
        }

        private static string CaptchaKey(string ipAddress) => "captcha-" + ipAddress;
    }
}