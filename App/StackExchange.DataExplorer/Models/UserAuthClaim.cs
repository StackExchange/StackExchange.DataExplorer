using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Models
{
    public class UserAuthClaim
    {
        private static readonly Regex subdomainUsernameRegex = new Regex("https?://(?<username>[^.]+)\\..*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex pathUsernameRegex = new Regex("https?://[^/]+/(?<username>[^/?]+)(?:/|\\?).*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Dictionary<string, AuthProvider> knownProviders = new Dictionary<string, AuthProvider>
        {
            { "openid.stackexchange.com", new AuthProvider("Stack Exchange") },
            { "me.yahoo.com", new AuthProvider("Yahoo") },
            { ".livejournal.com", new AuthProvider("LiveJournal", usernameRegex: subdomainUsernameRegex) },
            { ".wordpress.com", new AuthProvider("Wordpress", usernameRegex: subdomainUsernameRegex) },
            { ".blogspot.com", new AuthProvider("Blogger", usernameRegex: subdomainUsernameRegex) },
            { ".pip.verisignlabs.com", new AuthProvider("Verisign", usernameRegex: subdomainUsernameRegex) },
            { "openid.aol.com", new AuthProvider("AOL", usernameRegex: pathUsernameRegex) },
            { "www.google.com", new AuthProvider("Google") }
        };

        public enum ClaimType
        {
            OpenID = 1,
            Google = 2
        }

        public class Identifier
        {
            public readonly ClaimType Type;
            public readonly string Value;

            public Identifier(String value, ClaimType type)
            {
                Value = value;
                Type = type;
            }
        }

        public class AuthProvider
        {
            public string Name { get; private set; }
            public string IconClass { get; private set; }

            private readonly Regex usernameRegex;

            public AuthProvider(string name, bool hasIcon = true, Regex usernameRegex = null)
            {
                Name = name;

                if (hasIcon) 
                    IconClass = name.ToLower().Replace(" ", "");

                this.usernameRegex = usernameRegex;
            }

            public string GetUsername(string identifier)
            {
                if (usernameRegex == null)
                    return identifier;

                var username = usernameRegex.Match(identifier).Groups["username"];

                return username != null ? username.Value : identifier;
            }
        }

        private string display;
        private AuthProvider provider;

        public int Id { get; set; }
        public int UserId { get; set; }
        public string ClaimIdentifier { get; set; }
        public bool IsSecure { get; set; }
        public ClaimType IdentifierType { get; set; }
        public string Display
        {
            get { return display ?? Provider.GetUsername(ClaimIdentifier); }
            set { display = value; }
        }

        public AuthProvider Provider {
            get
            {
                if (provider == null)
                {
                    switch (IdentifierType)
                    {
                        case ClaimType.Google:
                            provider = knownProviders["www.google.com"];
                            break;
                        // case ClaimType.OpenID
                        default:
                            var uri = new Uri(ClaimIdentifier);
                            var host = uri.Host;
                            var subdomainIndex = host.IndexOf('.');

                            if (!knownProviders.ContainsKey(host) && subdomainIndex != -1 && subdomainIndex != host.LastIndexOf('.'))
                            {
                                host = host.Substring(subdomainIndex);
                            }

                            provider = knownProviders.ContainsKey(host) ? knownProviders[host] : new AuthProvider(uri.Host, hasIcon: false);
                            break;
                    }
                }

                return provider;
            }
        }
    }
}