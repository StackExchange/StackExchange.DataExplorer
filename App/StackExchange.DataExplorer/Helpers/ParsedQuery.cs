using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StackExchange.DataExplorer.Helpers
{
    public class ParsedQuery
    {
        public const string DEFAULT_NAME = "Enter Query Title";
        public const string DEFAULT_DESCRIPTION = "Enter Query Description";

        private static readonly Regex ParamsRegex = new Regex("##([a-zA-Z0-9]+):?([a-zA-Z]+)?##", RegexOptions.Compiled);

        private static readonly Regex ValidIntRegex = new Regex(@"\A[0-9]+\Z",
                                                                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex ValidFloatRegex = new Regex(@"\A[0-9]+(\.[0-9]+)?\Z",
                                                                  RegexOptions.Compiled | RegexOptions.Multiline);


        private static readonly ParameterType[] ParameterTypes =
            new[]
                {
                    new ParameterType("", _ => true, _ => _),
                    new ParameterType("int", data => ValidIntRegex.IsMatch(data), _ => _),
                    new ParameterType("string", _ => true, data => string.Format("'{0}'", data.Replace("'", "''"))),
                    new ParameterType("float", data => ValidFloatRegex.IsMatch(data), _ => _)
                };

        private static readonly Regex SplitOnGoRegex = new Regex(@"^\s*GO\s*$",
                                                                 RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public ParsedQuery(string sql, NameValueCollection requestParams, bool executionPlan, bool crossSite, bool excludeMetas) : this(sql, requestParams)
        {
            ExecutionPlan = executionPlan;
            CrossSite = crossSite;
            ExcludeMetas = excludeMetas;
        }

        public ParsedQuery(string sql, NameValueCollection requestParams)
        {
            Parse(sql, requestParams);
        }

        public static string DefaultComment
        {
            get { return string.Format("-- {0}\n-- {1}\n\n", DEFAULT_NAME, DEFAULT_DESCRIPTION); }
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

        public bool AllParamsSet { get; private set; }

        private bool executionPlan = false;

        /// <summary>
        /// Whether or not running this query should produce an execution plan
        /// </summary>
        public bool ExecutionPlan {
            get
            {
                return !CrossSite && executionPlan;
            }

            private set
            {
                executionPlan = value;
            }
        }

        /// <summary>
        /// Whether or not this query should be executed across all sites
        /// </summary>
        public bool CrossSite { get; private set; }

        private bool excludeMetas = false;

        /// <summary>
        /// Whether or not this query should exclude meta sites, if it should be executed across all sites
        /// </summary>
        public bool ExcludeMetas {
            get {
                return CrossSite && excludeMetas;
            }

            private set
            {
                excludeMetas = value;
            }
        }

        public string ErrorMessage { get; private set; }


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


        /// <summary>
        /// Sometimes our execution SQL contains GO in that case this can be split into batches
        ///  so the engine can execute each batch seperately
        /// </summary>
        public IEnumerable<string> ExecutionSqlBatches
        {
            get
            {
                foreach (string str in SplitOnGoRegex.Split(ExecutionSql))
                {
                    string trimmed = str.Trim();
                    if (trimmed != "")
                    {
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

        private void Parse(string rawSql, NameValueCollection requestParams)
        {
            var description = new StringBuilder();
            bool gotDescription = false;

            bool commentParsed = false;

            var sqlWithoutComment = new StringBuilder();

            bool gotName = false;

            foreach (string line in rawSql.Split('\n'))
            {
                if (commentParsed || !line.StartsWith("--"))
                {
                    commentParsed = true;
                    sqlWithoutComment.Append(line).Append("\n");
                    continue;
                }

                string trimmed = line.Substring(2).Trim();

                if (!gotName)
                {
                    gotName = true;
                    if (trimmed != DEFAULT_NAME)
                    {
                        Name = trimmed;
                    }
                }
                else
                {
                    if (trimmed == DEFAULT_DESCRIPTION)
                    {
                        continue;
                    }

                    if (gotDescription)
                    {
                        description.Append('\n');
                    }
                    description.Append(trimmed);
                    gotDescription = true;
                }
            }

            if (gotDescription)
            {
                Description = description.ToString();
            }

            RawSql = Normalize(rawSql);
            Sql = Normalize(sqlWithoutComment.ToString());

            List<string> errors;
            ExecutionSql = SubstituteParams(Sql, requestParams, out errors);

            AllParamsSet = ParamsRegex.Matches(ExecutionSql).Count == 0 && errors.Count == 0;

            if (errors.Count > 0)
            {
                ErrorMessage = string.Join("\n", errors);
            }

            ExecutionHash = Util.GetMD5(ExecutionSql);
            Hash = Util.GetMD5(Sql);
        }

        private string SubstituteParams(string sql, NameValueCollection requestParams, out List<string> errorCollection)
        {
            errorCollection = new List<string>();

            if (requestParams == null)
            {
                return sql;
            }

            MatchCollection matches = ParamsRegex.Matches(sql);

            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value;
                string type = match.Groups[2].Value;
                string subst = requestParams[name];

                if (string.IsNullOrEmpty(subst))
                {
                    continue;
                }

                if (!CheckIfTypeIsKnown(type))
                {
                    errorCollection.Add(string.Format("Unknown parameter type {0}!", type));
                    continue;
                }

                if (!ValidateType(type, subst))
                {
                    errorCollection.Add(string.Format("Expected {0} to be of type {1}!", name, type));
                    continue;
                }

                subst = EncodeType(type, subst);

                string param;
                if (string.IsNullOrEmpty(type))
                {
                    param = "##" + name + "##";
                }
                else
                {
                    param = "##" + name + ":" + type + "##";
                }
                sql = sql.Replace(param, subst);
            }

            return sql;
        }

        private static bool CheckIfTypeIsKnown(string type)
        {
            return ParameterTypes.Any(p => p.TypeName == type);
        }

        private static bool ValidateType(string type, string data)
        {
            ParameterType parameterType = ParameterTypes.First(p => p.TypeName == type);
            return parameterType.Validator(data);
        }

        private static string EncodeType(string type, string data)
        {
            ParameterType parameterType = ParameterTypes.First(p => p.TypeName == type);
            return parameterType.Encoder(data);
        }


        private string Normalize(string sql)
        {
            var buffer = new StringBuilder();
            bool started = false;
            foreach (string line in sql.Split('\n'))
            {
                string current = line;

                if (current.EndsWith("\r"))
                {
                    current = current.Substring(0, current.Length - 1);
                }

                if (current.Length == 0 && !started)
                {
                    continue;
                }

                buffer.Append(current).Append("\n");
                started = true;
            }

            string normalized = buffer.ToString();
            int endTrim = normalized.Length - 1;
            while (endTrim > 0 && normalized[endTrim] == '\n')
            {
                endTrim--;
            }

            return normalized.Substring(0, endTrim + 1);
        }

        #region Nested type: ParameterType

        private class ParameterType
        {
            public ParameterType(string typeName, Func<string, bool> validator, Func<string, string> encoder)
            {
                TypeName = typeName;
                Encoder = encoder;
                Validator = validator;
            }

            public Func<string, bool> Validator { get; private set; }
            public Func<string, string> Encoder { get; private set; }
            public string TypeName { get; private set; }
        }

        #endregion
    }
}