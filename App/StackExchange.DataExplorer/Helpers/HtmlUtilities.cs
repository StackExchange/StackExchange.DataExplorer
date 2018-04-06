using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using MarkdownSharp;

namespace StackExchange.DataExplorer.Helpers
{
    public static class HtmlUtilities
    {
        private static readonly Regex _invalidXMLChars =
            new Regex(
                @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
                RegexOptions.Compiled);
        
        private static readonly Regex _urlprotocolSafe = new Regex(@"^https?://", RegexOptions.Compiled);
        
        private static readonly Regex _sanitizeUrlAllowSpaces = new Regex(@"[^-a-z0-9+&@#/%?=~_|!:,.;\(\) ]",
                                                                          RegexOptions.IgnoreCase |
                                                                          RegexOptions.Compiled);

        private static readonly Regex _tags = new Regex("<[^>]*(>|$)",
                                                        RegexOptions.Singleline | RegexOptions.ExplicitCapture |
                                                        RegexOptions.Compiled);

        private static readonly Regex _whitelist =
            new Regex(
                @"
            ^</?(b(lockquote)?|code|d(d|t|l|el)|em|h(1|2|3)|i|kbd|li|ol|p(re)?|s(ub|up|trong|trike)?|ul)>$|
            ^<(b|h)r\s?/?>$",
                RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled |
                RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _whitelist_a =
            new Regex(
                @"
            ^<a\s
            href=""(\#\d+|(https?|ftp)://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+)""
            (\stitle=""[^""<>]+"")?\s?>$|
            ^</a>$",
                RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled |
                RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _whitelist_img =
            new Regex(
                @"
            ^<img\s
            src=""https?://[-a-z0-9+&@#/%?=~_|!:,.;\(\)]+""
            (\swidth=""\d{1,3}"")?
            (\sheight=""\d{1,3}"")?
            (\salt=""[^""<>]*"")?
            (\stitle=""[^""<>]*"")?
            \s?/?>$",
                RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled |
                RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _namedtags = new Regex
            (@"</?(?<tagname>\w+)[^>]*(\s|$|>)",
             RegexOptions.Singleline | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static readonly Regex _removeProtocolDomain = new Regex(@"http://[^/]+",
                                                                        RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        // filters control characters but allows only properly-formed surrogate sequences

        /// <summary>
        /// removes any unusual unicode characters that can't be encoded into XML
        /// </summary>
        public static string RemoveInvalidXMLChars(string text) => text.IsNullOrEmpty() ? "" : _invalidXMLChars.Replace(text, "");
        
        /// <summary>
        /// returns Html Encoded string
        /// </summary>
        public static string Encode(string html) => HttpUtility.HtmlEncode(html);

        /// <summary>
        /// returns Url Encoded string
        /// </summary>
        public static string UrlEncode(string html) => HttpUtility.UrlEncode(html);
        
        /// <summary>
        /// returns "safe" URL, stripping anything outside normal charsets for URL
        /// </summary>
        public static string SanitizeUrlAllowSpaces(string url) => url.IsNullOrEmpty() ? url : _sanitizeUrlAllowSpaces.Replace(url, "");
        
        /// <summary>
        /// sanitize any potentially dangerous tags from the provided raw HTML input using 
        /// a whitelist based approach, leaving the "safe" HTML tags
        /// CODESNIPPET:4100A61A-1711-4366-B0B0-144D1179A937
        /// </summary>
        public static string Sanitize(string html)
        {
            if (html.IsNullOrEmpty()) return html;

            // match every HTML tag in the input
            MatchCollection tags = _tags.Matches(html);
            for (int i = tags.Count - 1; i > -1; i--)
            {
                var tag = tags[i];
                var tagname = tag.Value.ToLowerInvariant();

                if (!(_whitelist.IsMatch(tagname) || _whitelist_a.IsMatch(tagname) || _whitelist_img.IsMatch(tagname)))
                {
                    html = html.Remove(tag.Index, tag.Length);
                    Debug.WriteLine("tag sanitized: " + tagname);
                }
            }

            return html;
        }

        /// <summary>
        /// process HTML so it is safe for display and free of XSS vulnerabilities
        /// </summary>
        public static string Safe(string html)
        {
            if (html.IsNullOrEmpty()) return html;
            html = Sanitize(html);
            html = BalanceTags(html);
            return html;
        }

        /// <summary>
        /// ensures <code>url</code> has a valid protocol for being used in a link somewhere
        /// </summary>
        /// <param name="url">the url to check</param>
        /// <returns>the processed url</returns>
        public static string SafeProtocol(string url)
        {
            if (!_urlprotocolSafe.IsMatch(url))
            {
                url = "http://" + url;
            }

            return url;
        }

        /// <summary>
        /// attempt to balance HTML tags in the html string
        /// by removing any unmatched opening or closing tags
        /// IMPORTANT: we *assume* HTML has *already* been 
        /// sanitized and is safe/sane before balancing!
        /// 
        /// CODESNIPPET: A8591DBA-D1D3-11DE-947C-BA5556D89593
        /// </summary>
        public static string BalanceTags(string html)
        {
            if (html.IsNullOrEmpty()) return html;

            // convert everything to lower case; this makes
            // our case insensitive comparisons easier
            MatchCollection tags = _namedtags.Matches(html.ToLowerInvariant());

            // no HTML tags present? nothing to do; exit now
            int tagcount = tags.Count;
            if (tagcount == 0) return html;

            const string ignoredtags = "<p><img><br><li><hr>";
            var tagpaired = new bool[tagcount];
            var tagremove = new bool[tagcount];

            // loop through matched tags in forward order
            for (int ctag = 0; ctag < tagcount; ctag++)
            {
                var tagname = tags[ctag].Groups["tagname"].Value;

                // skip any already paired tags
                // and skip tags in our ignore list; assume they're self-closed
                if (tagpaired[ctag] || ignoredtags.Contains("<" + tagname + ">"))
                    continue;

                var tag = tags[ctag].Value;
                var match = -1;

                if (tag.StartsWith("</"))
                {
                    // this is a closing tag
                    // search backwards (previous tags), look for opening tags
                    for (int ptag = ctag - 1; ptag >= 0; ptag--)
                    {
                        string prevtag = tags[ptag].Value;
                        if (!tagpaired[ptag] && prevtag.Equals("<" + tagname, StringComparison.InvariantCulture))
                        {
                            // minor optimization; we do a simple possibly incorrect match above
                            // the start tag must be <tag> or <tag{space} to match
                            if (prevtag.StartsWith("<" + tagname + ">") || prevtag.StartsWith("<" + tagname + " "))
                            {
                                match = ptag;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // this is an opening tag
                    // search forwards (next tags), look for closing tags
                    for (int ntag = ctag + 1; ntag < tagcount; ntag++)
                    {
                        if (!tagpaired[ntag] &&
                            tags[ntag].Value.Equals("</" + tagname + ">", StringComparison.InvariantCulture))
                        {
                            match = ntag;
                            break;
                        }
                    }
                }

                // we tried, regardless, if we got this far
                tagpaired[ctag] = true;
                if (match == -1)
                    tagremove[ctag] = true; // mark for removal
                else
                    tagpaired[match] = true; // mark paired
            }

            // loop through tags again, this time in reverse order
            // so we can safely delete all orphaned tags from the string
            for (int ctag = tagcount - 1; ctag >= 0; ctag--)
            {
                if (tagremove[ctag])
                {
                    html = html.Remove(tags[ctag].Index, tags[ctag].Length);
                    Debug.WriteLine("unbalanced tag removed: " + tags[ctag]);
                }
            }

            return html;
        }

        /// <summary>
        /// provided a NON-ENCODED url, returns a properly (cough) encoded return URL
        /// </summary>
        public static string GetReturnUrl(string url)
        {
            // prevent double-returning
            if (QueryStringContains(url, Keys.ReturnUrl))
                url = QueryStringRemove(url, Keys.ReturnUrl);

            // remove session key from return URL, if present
            if (QueryStringContains(url, Keys.Session))
                url = QueryStringRemove(url, Keys.Session);

            // remove the http://example.com part of the url
            url = _removeProtocolDomain.Replace(url, "");

            // allow only whitelisted URL characters, plus spaces
            url = SanitizeUrlAllowSpaces(url);

            // encode it for the URL
            return HttpUtility.UrlEncode(url);
        }

        /// <summary>
        /// fast (and maybe a bit inaccurate) check to see if the querystring contains the specified key
        /// </summary>
        public static bool QueryStringContains(string url, string key) => url.Contains(key + "=");

        /// <summary>
        /// removes the specified key, and any value, from the querystring. 
        /// for www.example.com/bar.foo?x=1&y=2&z=3 if you pass "y" you'll get back 
        /// www.example.com/bar.foo?x=1&z=3
        /// </summary>
        public static string QueryStringRemove(string url, string key) => url.IsNullOrEmpty() ? "" : Regex.Replace(url, @"[?&]" + key + "=[^&]*", "");
        
        /// <summary>
        /// Produces optional, URL-friendly version of a title, "like-this-one". 
        /// hand-tuned for speed, reflects performance refactoring contributed by John Gietzen (user otac0n) 
        /// </summary>
        public static string URLFriendly(string title)
        {
            if (title == null) return "";

            const int maxlen = 80;
            int len = title.Length;
            bool prevdash = false;
            var sb = new StringBuilder(len);
            string s;
            char c;

            for (int i = 0; i < len; i++)
            {
                c = title[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    prevdash = false;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    // tricky way to convert to lowercase
                    sb.Append((char) (c | 32));
                    prevdash = false;
                }
                else if (c == ' ' || c == ',' || c == '.' || c == '/' || c == '\\' || c == '-' || c == '_')
                {
                    if (!prevdash && sb.Length > 0)
                    {
                        sb.Append('-');
                        prevdash = true;
                    }
                }
                else if (c >= 128)
                {
                    s = c.ToString().ToLowerInvariant();
                    if ("àåáâäãåą".Contains(s))
                    {
                        sb.Append("a");
                    }
                    else if ("èéêëę".Contains(s))
                    {
                        sb.Append("e");
                    }
                    else if ("ìíîïı".Contains(s))
                    {
                        sb.Append("i");
                    }
                    else if ("òóôõöø".Contains(s))
                    {
                        sb.Append("o");
                    }
                    else if ("ùúûü".Contains(s))
                    {
                        sb.Append("u");
                    }
                    else if ("çćč".Contains(s))
                    {
                        sb.Append("c");
                    }
                    else if ("żźž".Contains(s))
                    {
                        sb.Append("z");
                    }
                    else if ("śşš".Contains(s))
                    {
                        sb.Append("s");
                    }
                    else if ("ñń".Contains(s))
                    {
                        sb.Append("n");
                    }
                    else if ("ýÿ".Contains(s))
                    {
                        sb.Append("y");
                    }
                    else if (c == 'ł')
                    {
                        sb.Append("l");
                    }
                    else if (c == 'đ')
                    {
                        sb.Append("d");
                    }
                    else if (c == 'ß')
                    {
                        sb.Append("ss");
                    }
                    else if (c == 'ğ')
                    {
                        sb.Append("g");
                    }
                    prevdash = false;
                }
                if (i == maxlen) break;
            }

            if (prevdash)
                sb.Length -= 1;
            
            return sb.ToString();
        }

        /// <summary>
        /// converts raw Markdown to HTML
        /// </summary>
        public static string RawToCooked(string rawText) => new Markdown().Transform(rawText);
    }
}
