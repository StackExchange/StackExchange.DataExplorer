using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Helpers {
    public class ParsedQuery {
        public const string DEFAULT_NAME = "Enter Query Title";
        public const string DEFAULT_DESCRIPTION = "Enter Query Description";

        public static string DefaultComment {
            get {
                return string.Format("-- {0}\n-- {1}\n\n",DEFAULT_NAME, DEFAULT_DESCRIPTION);
            }
        }

        static readonly Regex paramsRegex = new Regex("##([a-zA-Z0-9]+)##",RegexOptions.Compiled);

        public ParsedQuery(string sql, NameValueCollection requestParams) {
            Parse(sql, requestParams);
        }

        private void Parse(string rawSql, NameValueCollection requestParams) {

            var description = new StringBuilder();
            bool gotDescription = false;

            bool commentParsed = false;

            StringBuilder sqlWithoutComment = new StringBuilder();

            bool gotName = false;

            foreach (string line in rawSql.Split('\n')) {
                if (commentParsed || !line.StartsWith("--")) {
                    commentParsed = true;
                    sqlWithoutComment.Append(line).Append("\n");
                    continue;
                }

                var trimmed = line.Substring(2).Trim();

                if (!gotName) {
                    gotName = true;
                    if (trimmed != DEFAULT_NAME) {
                        Name = trimmed;
                    }
                } else {

                    if (trimmed == DEFAULT_DESCRIPTION) {
                        continue;
                    }

                    if (gotDescription) {
                        description.Append('\n'); 
                    }
                    description.Append(trimmed);
                    gotDescription = true;
                }
            }

            if (gotDescription) {
                Description = description.ToString();
            }

            RawSql = Normalize(rawSql);
            Sql = Normalize(sqlWithoutComment.ToString());

            ExecutionSql = SubstituteParams(Sql, requestParams);
            AllParamsSet = paramsRegex.Matches(ExecutionSql).Count == 0;

            ExecutionHash = Util.GetMD5(ExecutionSql);
            Hash = Util.GetMD5(Sql);
        }

        private string SubstituteParams(string sql, NameValueCollection requestParams) {

            if (requestParams == null) {
                return sql;
            }

            var matches = paramsRegex.Matches(sql);

            foreach (Match match in matches) {
                var name = match.Groups[1].Value;
                var subst = requestParams[name];
                if (string.IsNullOrEmpty(subst)) {
                    continue;
                }
                sql = sql.Replace("##" + name + "##", subst);
            }

            return sql;
        }

        private string Normalize(string sql) {
            var buffer = new StringBuilder();
            bool started = false;
            foreach (string line in sql.Split('\n')) {

                var current = line;

                if (current.EndsWith("\r")) {
                    current = current.Substring(0, current.Length - 1);
                }

                if (current.Length == 0 && !started) {
                    continue;
                }

                buffer.Append(current).Append("\n");
                started = true;
            }

            var normalized = buffer.ToString();
            var endTrim = normalized.Length - 1;
            while (endTrim > 0 && normalized[endTrim] == '\n') {
                endTrim--;
            }

            return normalized.Substring(0, endTrim + 1);
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

        public bool AllParamsSet { get; private set; }


        /// <summary>
        /// Original Sql with newlines normalized (no CR) not used for hashing
        /// </summary>
        public string RawSql { get; private set; }

        /// <summary>
        /// Sql with param placeholders, initial comment is stripped, newlines normalized and query is trimmed 
        ///   all final and initial empty lines are removed
        /// </summary>
        public string Sql { get; private set; }

        /// <summary>
        /// Sql we are supposed to execute, newlines are normalizes, initial comment is stripped
        ///  all final and initial empty lines are removed
        /// </summary>
        public string ExecutionSql { get; private set; }


        static readonly Regex SplitOnGoRegex = new Regex(@"^\s*GO\s*$",RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Sometimes our execution SQL contains GO in that case this can be split into batches
        ///  so the engine can execute each batch seperately
        /// </summary>
        public IEnumerable<string> ExecutionSqlBatches {
            get {
                foreach (var str in SplitOnGoRegex.Split(ExecutionSql)) {
                    var trimmed = str.Trim();
                    if (trimmed != "") {
                        yield return str;
                    }
                } 
            }
        }


        /// <summary>
        /// MD5 hash of Sql
        /// </summary>
        public Guid Hash { get; private set; }

        /// <summary>
        /// MD5 Hash of ExecutionSql 
        /// </summary>
        public Guid ExecutionHash { get; private set; }

    }
}