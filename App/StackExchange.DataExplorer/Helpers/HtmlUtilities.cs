using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using MarkdownSharp;

namespace StackExchange.DataExplorer.Helpers
{
    public static class HtmlUtilities
    {
        private const int FetchDefaultTimeoutMs = 2000;

        private static readonly Regex _invalidXMLChars =
            new Regex(
                @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
                RegexOptions.Compiled);

        private static readonly Regex _imagetags = new Regex(@"<img\s[^>]*(>|$)",
                                                             RegexOptions.IgnoreCase | RegexOptions.Singleline |
                                                             RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static readonly Regex _anchorTags = new Regex(@"<a\s[^>]*(>|$)",
                                                              RegexOptions.IgnoreCase | RegexOptions.Singleline |
                                                              RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        private static readonly Regex _anchorUrl = new Regex(@"href=""([^""]+)""",
                                                             RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _autolinks =
            new Regex(
                @"(\b(?<!""|>|;)(?:https?|ftp)://[A-Za-z0-9][-A-Za-z0-9+&@#/%?=~_|\[\]\(\)!:,.;]*[-A-Za-z0-9+&@#/%=~_|\[\]])",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _urlprotocol = new Regex(@"^(https?|ftp)://(www\.)?|(/$)", RegexOptions.Compiled);

        private static readonly Regex _urlprotocolSafe = new Regex(@"^https?://", RegexOptions.Compiled);

        private static readonly Regex _markdownMiniBold =
            new Regex(@"(?<=^|[\s,(])(?:\*\*|__)(?=\S)(.+?)(?<=\S)(?:\*\*|__)(?=[\s,?!.)]|$)", RegexOptions.Compiled);

        private static readonly Regex _markdownMiniItalic =
            new Regex(@"(?<=^|[\s,(])(?:\*|_)(?=\S)(.+?)(?<=\S)(?:\*|_)(?=[\s,?!.)]|$)", RegexOptions.Compiled);

        private static readonly Regex _markdownMiniCode = new Regex(@"(?<=\W|^)`(.+?)`(?=\W|$)", RegexOptions.Compiled);

        private static readonly Regex _markdownMiniLink = new Regex(
            @"(?<=\s|^)
\[
  (?<name>[^\]]+)
\]
\(
  (?<url>(https?|ftp)://[^)\s]+?)
  (
      \s(""|&quot;)
      (?<title>[^""]+)
      (""|&quot;)
  )?
\)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex _quoteSinglePair = new Regex(@"(?<![A-Za-z])'(.*?)'(?![A-Za-z])",
                                                                   RegexOptions.Compiled);

        private static readonly Regex _quoteSingle = new Regex(@"(?<=[A-Za-z0-9])'([A-Za-z]+)", RegexOptions.Compiled);
        private static readonly Regex _quoteDoublePair = new Regex(@"""(.*?)""", RegexOptions.Compiled);

        private static Regex _nofollow = new Regex(@"(<a\s+href=""([^""]+)"")([^>]*>)",
                                                   RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _sanitizeUrl = new Regex(@"[^-a-z0-9+&@#/%?=~_|!:,.;\(\)]",
                                                               RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        private static readonly Regex _amazonTest = new Regex(@"""http://www\.amazon\.",
                                                              RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _amazonRegex =
            new Regex(@"""http://www\.amazon\.\w{2,3}(?:\.\w{2,3})?/[^""]+/(\d{7,}X?)[^""]*?""",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string _amazonReplace = @"""http://rads.stackoverflow.com/amzn/click/$1""";

        /// <summary>
        /// returnurl=/path/
        /// </summary>
        public static string ReturnQueryString
        {
            get { return Keys.ReturnUrl + "=" + GetReturnUrl(HttpContext.Current.Request.Url.ToString()); }
        }

        /// <summary>
        /// remove any potentially dangerous tags from the provided raw HTML input
        /// </summary>
        public static string RemoveTags(string html)
        {
            if (html.IsNullOrEmpty()) return "";
            return _tags.Replace(html, "");
        }

        /// <summary>
        /// removes specified tag and any contents of that tag (intended for PRE, SCRIPT, etc)
        /// </summary>
        public static string RemoveTagContents(string tag, string html)
        {
            string pattern = "<{0}[^>]*>(.*?)</{0}>".Replace("{0}", tag);
            return Regex.Replace(html, pattern, "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }

        // filters control characters but allows only properly-formed surrogate sequences

        /// <summary>
        /// removes any unusual unicode characters that can't be encoded into XML
        /// </summary>
        public static string RemoveInvalidXMLChars(string text)
        {
            if (text.IsNullOrEmpty()) return "";
            return _invalidXMLChars.Replace(text, "");
        }

        /// <summary>
        /// remove image tags from the provided HTML input
        /// </summary>
        public static string RemoveImageTags(string html)
        {
            if (html.IsNullOrEmpty()) return "";
            return _imagetags.Replace(html, "");
        }

        /// <summary>
        /// returns true if the provided HTML input has an image tag
        /// </summary>
        public static bool HasImageTags(string html)
        {
            if (html.IsNullOrEmpty()) return false;
            return _imagetags.IsMatch(html);
        }

        /// <summary>
        /// returns # of anchor tags OF ANY KIND in the input html
        /// </summary>
        public static int HasAnchorTagsAny(string html)
        {
            if (html.IsNullOrEmpty()) return 0;
            return _anchorTags.Matches(html).Count;
        }

        /// <summary>
        /// returns # of anchor tags to sites outside our network in the input html
        /// </summary>
        public static int HasAnchorTags(string html)
        {
            if (html.IsNullOrEmpty()) return 0;
            int cnt = 0;
            string url;
            foreach (Match m in _anchorTags.Matches(html))
            {
                url = _anchorUrl.Match(m.Value).Groups[1].Value;
                if (true /*!SiteExtensions.IsInNetwork(url)*/) cnt++;
            }
            return cnt;
        }

        /// <summary>
        /// replace any text of the form example.com with a hyperlink; DANGER, does not nofollow
        /// intended for INTERNAL SITE USE ONLY
        /// </summary>
        public static string HyperlinkUrlsGenerously(string text)
        {
            if (text.IsNullOrEmpty()) return "";
            // bail if we already have a hyperlink
            if (HasAnchorTagsAny(text) > 0) return text;
            // go for it
            return Regex.Replace(text, @"([\w\-\d\.]+\.(com|net|org|edu))", "<a href=\"http://$1\">$1</a>");
        }

        /// <summary>
        /// returns true if the provided text contains a semi-valid URL
        /// </summary>
        public static bool IsUrl(string text)
        {
            return _autolinks.IsMatch(text);
        }

        /// <summary>
        /// auto-hyperlinks any *naked* URLs encountered in the text, with nofollow; 
        /// please note that valid HTML anchors will *not* be linked
        /// </summary>
        public static string HyperlinkUrls(string text, string cssclass, bool nofollow)
        {
            if (text.IsNullOrEmpty()) return "";

            string linkTemplate = @"<a href=""$1""";
            if (cssclass.HasValue())
                linkTemplate += " class=\"" + cssclass + "\"";
            if (nofollow)
                linkTemplate += @" rel=""nofollow""";
            linkTemplate += @">$2</a>";

            string url;
            string link;
            string linkTemplateBackup = linkTemplate;
            int offset = 0;
            const int maxlen = 50;

            foreach (Match m in _autolinks.Matches(text))
            {
                url = m.Value;
                linkTemplate = linkTemplateBackup;

                if (url.Length > maxlen)
                {
                    // if this is a stackoverflow-style URL, then let's have a mouseover title element
                    if (Regex.IsMatch(url, @"/questions/\d{3,}/"))
                    {
                        // extract the friendly text title
                        string title = Regex.Match(url, @"\d{3,}/([^/]+)").Groups[1].Value;
                        if (title.HasValue())
                            linkTemplate = linkTemplate.Replace(@">$2",
                                                                @" title=""" + UrlEncode(title).Replace("-", " ") +
                                                                @""">$2");
                    }

                    link = linkTemplate.Replace("$1", url).Replace("$2", ShortenUrl(url, maxlen));
                }
                else
                {
                    link = linkTemplate.Replace("$1", url).Replace("$2", RemoveUrlProtocol(url));
                }

                text = text.Substring(0, m.Index + offset) + link + text.Substring(m.Index + m.Length + offset);
                offset += (link.Length - m.Length);
            }

            return text;
        }

        /// <summary>
        /// auto-hyperlinks any URLs encountered in the text, with nofollow
        /// </summary>
        public static string HyperlinkUrls(string text)
        {
            return HyperlinkUrls(text, null);
        }

        /// <summary>
        /// auto-hyperlinks any URLs encountered in the text, with nofollow
        /// </summary>
        public static string HyperlinkUrls(string text, string cssclass)
        {
            return HyperlinkUrls(text, null, true);
        }


        /// <summary>
        /// makes a http://veryveryvery/very/very/very-long-url.html shorter for display purposes; 
        /// tries to break at slash borders
        /// </summary>
        public static string ShortenUrl(string url, int maxlen)
        {
            url = RemoveUrlProtocol(url);
            if (url.Length < maxlen) return url;

            for (int i = url.Length - 1; i > 0; i--)
            {
                if ((url[i] == '/') && (i < maxlen))
                    return url.Substring(0, i) + "/&hellip;";
            }

            return url.Substring(0, maxlen - 1) + "&hellip;";
        }

        /// <summary>
        /// removes the protocol (and trailing slash, if present) from the URL
        /// </summary>
        private static string RemoveUrlProtocol(string url)
        {
            return _urlprotocol.Replace(url, "");
        }

        /// <summary>
        /// returns Html Encoded string
        /// </summary>
        public static string Encode(string html)
        {
            return HttpUtility.HtmlEncode(html);
        }

        /// <summary>
        /// returns Url Encoded string
        /// </summary>
        public static string UrlEncode(string html)
        {
            return HttpUtility.UrlEncode(html);
        }

        /// <summary>
        /// tiny subset of Markdown: *italic* and __bold__ and `code` and [link](http://example.com "title") only  
        /// </summary>
        public static string MarkdownMini(string text)
        {
            if (text.IsNullOrEmpty()) return "";

            text = HttpUtility.HtmlEncode(text);

            bool hasEligibleChars = false;

            // for speed, quickly screen out strings that don't contain anything we can possibly work on
            char pc = ' ';
            foreach (char c in text)
            {
                if (c == '*' || c == '_' || c == '`' || (pc == ']' && c == '('))
                {
                    hasEligibleChars = true;
                    break;
                }
                pc = c;
            }

            if (!hasEligibleChars) return text;

            // replace any escaped characters, first, so we don't do anything with them
            text = text.Replace(@"\`", "&#96;");
            text = text.Replace(@"\*", "&#42;");
            text = text.Replace(@"\_", "&#95;");
            text = text.Replace(@"\[", "&#91;");
            text = text.Replace(@"\]", "&#93;");
            text = text.Replace(@"\(", "&#40;");
            text = text.Replace(@"\)", "&#41;");

            // deal with code block first, since it should be "protected" from any further encodings            
            foreach (object match in _markdownMiniCode.Matches(text))
            {
                string code = match.ToString();
                code = code.Substring(1, code.Length - 2);
                code = code.Replace("_", "&#95;");
                code = code.Replace("*", "&#42;");
                code = code.Replace("//", "&#47;&#47;");
                text = text.Replace(match.ToString(), "<code>" + code + "</code>");
            }
            //text = _markdownMiniCode.Replace(text, "<code>$1</code>");

            // must handle bold first (it's longer), then italic
            text = _markdownMiniBold.Replace(text, "<b>$1</b>");
            text = _markdownMiniItalic.Replace(text, "<i>$1</i>");

            text = _markdownMiniLink.Replace(text, new MatchEvaluator(MarkdownMiniLinkEvaluator));

            return text;
        }


        private static string MarkdownMiniLinkEvaluator(Match match)
        {
            string url = match.Groups["url"].Value;
            string name = match.Groups["name"].Value;
            string title = match.Groups["title"].Value;
            string link;

            url = SanitizeUrl(url);
            // we don't need to sanitize name here, as we encoded in the parent function

            if (title.HasValue())
            {
                title = title.Replace("\"", "");
                link = String.Format(@"<a href=""{0}"" title=""{2}"" rel=""nofollow"">{1}</a>", url, name, title);
            }
            else
            {
                link = String.Format(@"<a href=""{0}"" rel=""nofollow"">{1}</a>", url, name);
            }

            // if this is a link to a site in our network, it's whitelisted and safe to follow
            if (true /*SiteExtensions.IsInNetwork(url)*/)
                link = link.Replace(@" rel=""nofollow""", "");

            return link;
        }

        /// <summary>
        /// converts to fancy typographical HTML entity versions of "" and '' and -- and ...
        /// loosely based on rules at http://daringfireball.net/projects/smartypants/
        /// assumes NO HTML MARKUP TAGS inside the text!
        /// </summary>
        private static string SmartyPantsMini(string s)
        {
            bool hasEligibleChars = false;
            char p = ' ';

            // quickly screen out strings that don't contain anything we can possibly work on
            // the VAST majority of actual titles have none of these chars
            foreach (char c in s)
            {
                if ((p == '-' && c == '-') || c == '\'' || c == '"' || (p == '.' && c == '.') || c == '&')
                {
                    hasEligibleChars = true;
                    break;
                }
                p = c;
            }

            if (!hasEligibleChars) return s;

            // convert encoded quotes back to regular quotes for simplicity
            s = s.Replace("&quot;", @"""");

            // ... (or more) becomes &hellip;
            if (s.Contains("..."))
                s = Regex.Replace(s, @"\.{3,}", @"&hellip;");

            // --- or -- becomes &mdash;
            if (s.Contains("--"))
                s = Regex.Replace(s, @"---?(\s)", @"&mdash;$1");

            // "foo" becomes &ldquo;foo&rdquo;
            if (s.Contains("\""))
                s = _quoteDoublePair.Replace(s, "&ldquo;$1&rdquo;");

            // 'foo' becomes &lsquo;foo&rsquo;
            // A's and O'Malley becomes &rsquo;s
            if (s.Contains("'"))
            {
                s = _quoteSinglePair.Replace(s, "&lsquo;$1&rsquo;");
                s = _quoteSingle.Replace(s, "&rsquo;$1");
            }

            return s;
        }


        /// <summary>
        /// encodes any HTML, also adds any fancy typographical entities versions of "" and '' and -- and ...
        /// </summary>
        public static string EncodeFancy(string html)
        {
            if (html.IsNullOrEmpty()) return html;
            return SmartyPantsMini(Encode(html));
        }


        /// <summary>
        /// removes any &gt; or &lt; characters from the input
        /// </summary>
        public static string RemoveTagChars(string s)
        {
            if (s.IsNullOrEmpty()) return s;
            return s.Replace("<", "").Replace(">", "");
        }

        /// <summary>
        /// returns "safe" URL, stripping anything outside normal charsets for URL
        /// </summary>
        public static string SanitizeUrl(string url)
        {
            if (url.IsNullOrEmpty()) return url;
            return _sanitizeUrl.Replace(url, "");
        }

        /// <summary>
        /// returns "safe" URL, stripping anything outside normal charsets for URL
        /// </summary>
        public static string SanitizeUrlAllowSpaces(string url)
        {
            if (url.IsNullOrEmpty()) return url;
            return _sanitizeUrlAllowSpaces.Replace(url, "");
        }


        /// <summary>
        /// sanitize any potentially dangerous tags from the provided raw HTML input using 
        /// a whitelist based approach, leaving the "safe" HTML tags
        /// CODESNIPPET:4100A61A-1711-4366-B0B0-144D1179A937
        /// </summary>
        public static string Sanitize(string html)
        {
            if (html.IsNullOrEmpty()) return html;

            string tagname;
            Match tag;

            // match every HTML tag in the input
            MatchCollection tags = _tags.Matches(html);
            for (int i = tags.Count - 1; i > -1; i--)
            {
                tag = tags[i];
                tagname = tag.Value.ToLowerInvariant();

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

            string tagname;
            string tag;
            const string ignoredtags = "<p><img><br><li><hr>";
            int match;
            var tagpaired = new bool[tagcount];
            var tagremove = new bool[tagcount];

            // loop through matched tags in forward order
            for (int ctag = 0; ctag < tagcount; ctag++)
            {
                tagname = tags[ctag].Groups["tagname"].Value;

                // skip any already paired tags
                // and skip tags in our ignore list; assume they're self-closed
                if (tagpaired[ctag] || ignoredtags.Contains("<" + tagname + ">"))
                    continue;

                tag = tags[ctag].Value;
                match = -1;

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
        public static bool QueryStringContains(string url, string key)
        {
            return url.Contains(key + "=");
        }

        /// <summary>
        /// removes the specified key, and any value, from the querystring. 
        /// for www.example.com/bar.foo?x=1&y=2&z=3 if you pass "y" you'll get back 
        /// www.example.com/bar.foo?x=1&z=3
        /// </summary>
        public static string QueryStringRemove(string url, string key)
        {
            if (url.IsNullOrEmpty()) return "";
            return Regex.Replace(url, @"[?&]" + key + "=[^&]*", "");
        }

        /// <summary>
        /// returns the value, if any, of the specified key in the querystring
        /// </summary>
        public static string QueryStringValue(string url, string key)
        {
            if (url.IsNullOrEmpty()) return "";
            return Regex.Match(url, key + "=.*").ToString().Replace(key + "=", "");
        }

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
                    else if ("ýŸ".Contains(s))
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
        public static string RawToCooked(string rawText)
        {
            return (new Markdown()).Transform(rawText);
        }

        /// <summary>
        /// to be called after converting markdown to HTML;
        /// does any post-processing HTML fixups we deem necessary
        /// </summary>
        private static string PostProcessHtml(string html)
        {
            html = PostProcessAmazon(html);
            return html;
        }

        private static string PostProcessAmazon(string html)
        {
            if (!_amazonTest.IsMatch(html)) return html;
            return _amazonRegex.Replace(html, _amazonReplace);
        }

        /// <summary>
        /// remove entities such as "&gt;" or "&quot;"
        /// </summary>
        public static string RemoveEntities(string html)
        {
            return Regex.Replace(html, @"&([^; ]+);", "");
        }

        /// <summary>
        /// remove double-encoded entities; translates "&amp;gt;" to "&gt;"
        /// </summary>
        public static string DecodeEntities(string html)
        {
            return Regex.Replace(html, @"&amp;([^; ]+);", @"&$1;");
        }

        public static string MakeClasses(Dictionary<string, bool> possibilities)
        {
            var buffer = new StringBuilder();

            foreach (var possibility in possibilities)
            {
                if (possibility.Value)
                {
                    buffer.Append(" ").Append(possibility.Key);
                }
            }

            return buffer.ToString();
        }
    }
}
