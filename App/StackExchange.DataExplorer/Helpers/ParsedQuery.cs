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
        private static readonly Regex ParamsRegex = new Regex(
            @"##(?<name>[a-zA-Z][a-zA-Z0-9]*)(?::(?<type>[a-zA-Z]+))?(?:\?(?<default>[^#]+))?##",
            RegexOptions.Compiled
        );

        private static readonly Regex QuotesRegex = new Regex(
            @"'[^']*'",
            RegexOptions.Compiled
        );

        private static readonly Regex ValidIntRegex = new Regex(
            @"\A[0-9]+\Z",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private static readonly Regex ValidFloatRegex = new Regex(
            @"\A[0-9]+(\.[0-9]+)?\Z",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private static readonly ParameterType[] ParameterTypes = new[]
        {
            new ParameterType("", _ => true, _ => _),
            new ParameterType("int", data => ValidIntRegex.IsMatch(data), _ => _),
            new ParameterType("string", _ => true, data => string.Format("'{0}'", data.Replace("'", "''"))),
            new ParameterType("float", data => ValidFloatRegex.IsMatch(data), _ => _)
        };

        private static readonly Regex SplitOnGoRegex = new Regex(
            @"^\s*GO\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

        public ParsedQuery(string sql, NameValueCollection requestParams, bool crossSite, bool excludeMetas)
            : this(sql, requestParams, false, crossSite, excludeMetas)
        {

        }

        public ParsedQuery(string sql, NameValueCollection requestParams, bool executionPlan, bool crossSite, bool excludeMetas)
            : this(sql, requestParams)
        {
            IncludeExecutionPlan = executionPlan;
            IsCrossSite = crossSite;
            ExcludesMetas = excludeMetas;
        }

        public ParsedQuery(string sql, NameValueCollection requestParams)
        {
            Parameters = new Dictionary<string, QueryParameter>();
            Parse(sql, requestParams);
        }

        public List<string> Errors { get; private set; }

        public Dictionary<string, QueryParameter> Parameters { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public bool AllParamsSet { get; private set; }

        private bool includeExecutionPlan = false;

        /// <summary>
        /// Whether or not running this query should produce an execution plan
        /// </summary>
        public bool IncludeExecutionPlan {
            get
            {
                return !IsCrossSite && includeExecutionPlan;
            }

            private set
            {
                includeExecutionPlan = value;
            }
        }

        /// <summary>
        /// Whether or not this query should be executed across all sites
        /// </summary>
        public bool IsCrossSite { get; private set; }

        private bool excludesMetas = false;

        /// <summary>
        /// Whether or not this query should exclude meta sites, if it should be executed across all sites
        /// </summary>
        public bool ExcludesMetas {
            get {
                return IsCrossSite && excludesMetas;
            }

            private set
            {
                excludesMetas = value;
            }
        }

        public string ErrorMessage { get; private set; }

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

        private void Parse(string sql, NameValueCollection requestParams)
        {            
            Sql = Normalize(sql.Trim());
            ExecutionSql = ReduceAndPopulate(Sql, requestParams);
            AllParamsSet = Errors.Count == 0;

            if (Errors.Count > 0)
            {
                ErrorMessage = string.Join("\n", Errors);
            }

            ExecutionHash = Util.GetMD5(ExecutionSql);
            Hash = Util.GetMD5(Sql);
        }

        private string SubstituteParams(string sql, NameValueCollection requestParams)
        {
            Errors = new List<string>();

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
                    Errors.Add(string.Format("Unknown parameter type {0}!", type));
                    continue;
                }

                if (!ValidateType(type, subst))
                {
                    Errors.Add(string.Format("Expected {0} to be of type {1}!", name, type));
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

        private string ReduceAndPopulate(string sql, NameValueCollection requestParams)
        {
            bool stringified = false;
            int commented = 0;
            string result = null;

            // The goal here is to reduce the SQL to as basic of a representation as possible,
            // without changing the actuale execution results.
            try
            {
                Errors = new List<string>();
                var buffer = new StringBuilder();
                Action<string, int> ParseLine = null;

                #region Inner Function: ParseLine

                ParseLine = (line, depth) =>
                {
                    int startComment, startString, startSingleComment, endComment, endString;

                    if (line.Length == 0 || (!stringified && line.Trim().Length == 0))
                    {
                        return;
                    }

                    if (depth == 10)
                    {
                        // Should never happen, but if we get this deep it's just safer to bail
                        throw new StackOverflowException("SQL reduction has likely gone wrong");
                    }
                    else
                    {
                        ++depth;
                    }

                    if (commented > 0)
                    {
                        endComment = line.IndexOf("*/");

                        if (endComment != -1)
                        {
                            commented += line.Substring(0, endComment).OccurencesOf("/*") - 1;
                            line = line.Substring(endComment + 2);

                            if (commented > 0)
                            {
                                ParseLine(line, depth);

                                return;
                            }
                        }
                        else
                        {
                            commented += line.OccurencesOf("/*");

                            return;
                        }
                    }
                    else if (stringified)
                    {
                        endString = line.IndexOf("'");

                        if (endString == -1)
                        {
                            buffer.Append(ScanSegment(line)).Append('\n');

                            return;
                        }

                        buffer.Append(ScanSegment(line.Substring(0, endString + 1)));

                        line = line.Substring(endString + 1);

                        if (line.Length > 0 && line[0] == '\'')
                        {
                            // We didn't actually end the string, because the single quote we
                            // indexed before was actually escaping this single quote.
                            buffer.Append('\'');
                            ParseLine(line.Substring(1), depth);

                            return;
                        }
                        else if (line.Length == 0)
                        {
                            // We run into spacing issues without this if this string terminates the line
                            buffer.Append(" ");
                        }

                        stringified = false;
                    }

                    List<string> substitutions = new List<string>();
                    string remainder = null;

                    line = QuotesRegex.Replace(line, (match) =>
                    {
                        substitutions.Add(match.Value);

                        return "~S" + (substitutions.Count - 1);
                    });

                    startSingleComment = line.IndexOf("--");
                    startSingleComment = startSingleComment == -1 ? line.Length : startSingleComment;
                    startString = line.IndexOf('\'', 0, startSingleComment);
                    startComment = line.IndexOf("/*", 0, startSingleComment);

                    if (startString != -1 || startComment != -1)
                    {
                        if ((startString < startComment && startString != -1) || startComment == -1)
                        {
                            stringified = true;
                        }
                        else
                        {
                            ++commented;
                            remainder = line.Substring(startComment + 2);
                            line = line.Substring(0, startComment);
                        }
                    }
                    else
                    {
                        line = line.Substring(0, startSingleComment);
                    }

                    for (int i = 0; i < substitutions.Count; ++i)
                    {
                        line = line.Replace("~S" + i, substitutions[i]);
                    }

                    if (!stringified)
                    {
                        line = line.Trim();
                    }
                    else if (startString != -1)
                    {
                        line = line.TrimStart();
                    }

                    if (line.Length > 0 || stringified)
                    {
                        buffer.Append(ScanSegment(line));

                        if (stringified)
                        {
                            buffer.Append('\n');
                        }
                        else
                        {
                            buffer.Append(' ');
                        }
                    }

                    if (remainder != null)
                    {
                        ParseLine(remainder, depth);
                    }
                };
                #endregion

                foreach (string line in sql.Split('\n'))
                {
                    ParseLine(line, 0);
                }

                result = buffer.ToString().Trim();

                foreach (string name in Parameters.Keys)
                {
                    var parameter = Parameters[name];
                    string value = requestParams[name];
                    ParameterType type = null;

                    if (!string.IsNullOrEmpty(parameter.Type))
                    {
                        type = GetType(parameter.Type);
                    }

                    if (!string.IsNullOrEmpty(parameter.Default) && type != null)
                    {
                        if (!type.Validator(parameter.Default))
                        {
                            Errors.Add(string.Format("Expected default value {0} for {1} to be of type {2}!",
                                parameter.Default, name, type.TypeName));

                            continue;
                        }
                    }

                    if (string.IsNullOrEmpty(value))
                    {
                        Errors.Add(string.Format("Value for {0} was empty or not provided.", name));

                        continue;
                    }

                    if (type != null)
                    {
                        if (!type.Validator(value))
                        {
                            Errors.Add(string.Format("Expected value for {0} to be of type {1}!",
                                name, type.TypeName));

                            continue;
                        }

                        value = type.Encoder(value);
                    }

                    result = result.Replace("##" + name + "##", value);
                }
            }
            catch (StackOverflowException)
            {
                result = SubstituteParams(sql, requestParams);
            }

            return result;
        }

        private string ScanSegment(string segment) {
            MatchCollection matches = ParamsRegex.Matches(segment);
            string scanned = segment;

            foreach (Match match in matches)
            {
                string name = match.Groups["name"].Value;
                QueryParameter parameter;

                if (Parameters.ContainsKey(name))
                {
                    parameter = Parameters[name];
                }
                else
                {
                    parameter = new QueryParameter();
                }

                if (match.Groups["type"].Success)
                {
                    string type = match.Groups["type"].Value.ToLower();

                    if (!CheckIfTypeIsKnown(type))
                    {
                        Errors.Add(string.Format("{0} has unknown parameter type {1}!", name, type));

                        continue;
                    }

                    parameter.Type = type;
                }

                if (match.Groups["default"].Success)
                {
                    parameter.Default = match.Groups["default"].Value;
                }

                Parameters[name] = parameter;
                scanned = scanned.ReplaceFirst(match.Value, "##" + name + "##");
            }

            return scanned;
        }

        private static bool CheckIfTypeIsKnown(string type)
        {
            return ParameterTypes.Any(p => p.TypeName == type);
        }

        private static ParameterType GetType(string type)
        {
            return ParameterTypes.First(p => p.TypeName == type);
        }

        private static bool ValidateType(string type, string data)
        {
            return GetType(type).Validator(data);
        }

        private static string EncodeType(string type, string data)
        {
            return GetType(type).Encoder(data);
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

        public struct QueryParameter
        {
            public string Type { get; set; }
            public string Default { get; set; }
            public string Value { get; set; }
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