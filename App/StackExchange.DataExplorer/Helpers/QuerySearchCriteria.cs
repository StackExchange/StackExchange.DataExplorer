using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace StackExchange.DataExplorer.Helpers
{
    public class QuerySearchCriteria
    {
        public const int MIN_SEARCH_CHARS = 2;

        public string RawInput { get; private set; }
        public bool IsValid { get; private set; }

        public string SearchTerm { get; private set; }
        public bool IsFeatured { get; private set; }

        public QuerySearchCriteria(string searchString)
        {
            RawInput = searchString;
            IsValid = false;

            SearchTerm = string.Empty;
            IsFeatured = false;

            _ProcessRawInput();
        }

        private void _ProcessRawInput()
        {
            if (RawInput == null) return;

            string s = (RawInput ?? string.Empty).Trim();

            IsFeatured = _MatchIsFeatured(ref s);

            if (s.Length >= MIN_SEARCH_CHARS)
            {
                IsValid = true;
                SearchTerm = s;
            }
        }

        private bool _MatchIsFeatured(ref string s)
        {
            string pattern = @"\bisfeatured:1\b";
            RegexOptions ro = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            Match match = null;
            bool matched = false;

            while ((match = Regex.Match(s, pattern, ro)).Success)
            {
                matched = true;
                s = Regex.Replace(s, pattern, string.Empty, ro).Trim();
            }

            return matched;
        }
    }
}