using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace StackExchange.DataExplorer.Helpers
{
    public static class Captcha
    {
        private static readonly HttpClient _cachedClient = new HttpClient();
        public static async Task<bool> ChallengeIsvalidAsync(NameValueCollection form)
        {
            if (IsPassedForCurrentUser())
            {
                return true;
            }

            var challenge = form.Get("g-recaptcha-response", "");
            bool userIsValidated = false;

            if (challenge.HasValue())
            {
                var response = await _cachedClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = AppSettings.RecaptchaPrivateKey,
                    ["response"] = challenge,
                    ["remoteip"] = Current.RemoteIP
                }));

                if (response.IsSuccessStatusCode)
                {
                    using (var content = response.Content)
                    {
                        var responseString = await content.ReadAsStringAsync();
                        userIsValidated = ((CaptchaResponse)JsonConvert.DeserializeObject(responseString, typeof(CaptchaResponse))).Success;
                    }
                }
                else
                {
                    // recaptcha is down - if the challenge had some length (it's usually a massive hash), allow the action - spam will be destroyed by the community
                    userIsValidated = challenge.Length >= 30;
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