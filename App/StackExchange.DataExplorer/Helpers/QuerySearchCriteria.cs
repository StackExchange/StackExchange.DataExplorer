using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Helpers
{
    public class QuerySearchCriteria
    {
        public const int MIN_SEARCH_CHARS = 2;

        public string RawInput { get; }
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

            var s = (RawInput ?? string.Empty).Trim();

            IsFeatured = _MatchIsFeatured(ref s);

            if (s.Length >= MIN_SEARCH_CHARS)
            {
                IsValid = true;
                SearchTerm = s;
            }
        }

        private static readonly Regex _featuredRegex = new Regex(@"\bisfeatured:1\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private bool _MatchIsFeatured(ref string s)
        {
            bool matched = false;
            while (_featuredRegex.Match(s).Success)
            {
                matched = true;
                s = _featuredRegex.Replace(s, "").Trim();
            }

            return matched;
        }
    }
}