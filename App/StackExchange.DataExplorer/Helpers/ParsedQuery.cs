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
        [Flags]
        private enum StateFlags
        {
            Literal = 1 << 0,
            String = 1 << 1,
            Comment = 1 << 2,
            Multiline = 1 << 3
        }

        private static readonly Regex _whitespaceRegex = new Regex(@"(?<!^)(?<whitespace>[\n ])\1+(?!$)", RegexOptions.Compiled);
        private static readonly Regex _boundarySpacesRegex = new Regex(@"(?<!\A)^ *| *$(?!\Z)", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex _paramsRegex = new Regex(
            @"##(?<name>[a-zA-Z][a-zA-Z0-9]*)(?::(?<type>[a-zA-Z]+))?(?:\?(?<default>[^#]+))?##",
            RegexOptions.Compiled
        );

        private static readonly Regex _descriptionRegex = new Regex(
            @"-- *(?<name>[a-zA-Z][A-Za-z0-9]*) *: *(?<label>[^""]+)(?:""(?<description>[^""]+)"")?",
            RegexOptions.Compiled
        );

        private static readonly Regex ValidIntRegex = new Regex( @"\A-?[0-9]+\Z", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex ValidFloatRegex = new Regex(
            @"\A-?[0-9]+(\.[0-9]+)?\Z",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private static readonly ParameterType[] ParameterTypes = {
            new ParameterType("", _ => true, _ => _),
            new ParameterType("int", data => ValidIntRegex.IsMatch(data), _ => _),
            new ParameterType("string", _ => true, data => string.Format("'{0}'", data.Replace("'", "''"))),
            new ParameterType("float", data => ValidFloatRegex.IsMatch(data), _ => _)
        };

        private static readonly Regex SplitOnGoRegex = new Regex(
            @"^\s*GO\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );

        public ParsedQuery(string sql, NameValueCollection requestParams, bool executionPlan = false, TargetSites targetSites = TargetSites.Current)
        {
            IncludeExecutionPlan = executionPlan;
            TargetSites = targetSites;
            Parameters = new Dictionary<string, QueryParameter>();
            Errors = new List<string>();
            Parse(sql, requestParams);
        }

        public List<string> Errors { get; }
        public string ErrorMessage => string.Join("\n", Errors);

        public Dictionary<string, QueryParameter> Parameters { get; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        public bool IsExecutionReady { get; private set; }

        private bool _includeExecutionPlan;

        /// <summary>
        /// Whether or not running this query should produce an execution plan
        /// </summary>
        public bool IncludeExecutionPlan
        {
            get { return TargetSites == TargetSites.Current && _includeExecutionPlan; }
            private set { _includeExecutionPlan = value; }
        }

        /// <summary>
        /// Whether or not this query should be executed across all sites
        /// </summary>
        public TargetSites TargetSites { get; }

        /// <summary>
        /// Sql with param placeholders, initial comment is stripped, newlines normalized and query is trimmed 
        ///   all final and initial empty lines are removed
        /// </summary>
        private readonly StringBuilder _sql = new StringBuilder();
        public string Sql => _sql.ToString().Trim();

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
                        yield return trimmed;
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
            var prepared = PrepareSQL(sql);

            if (Errors.Count == 0)
            {
                ExecutionSql = SubstituteParameters(prepared, requestParams).Trim();
            }

            IsExecutionReady = Errors.Count == 0;
            Hash = Util.GetMD5(Sql);

            if (IsExecutionReady)
            {
                ExecutionHash = Util.GetMD5(ExecutionSql);
            }
        }

        private StringBuilder PrepareSQL(string raw)
        {
            var state = StateFlags.Literal;
            var token = new StringBuilder();
            var executionSql = new StringBuilder();
            char? current, next;
            int depth = 0, i = 0;

            while (i < raw.Length)
            {
                current = raw[i];
                next = ++i < raw.Length ? raw[i] : (char?)null;

                bool transition = true, skipNext = false;
                var savedState = state;

                if (state.HasFlag(StateFlags.Literal))
                {
                    if (current == '\'')
                    {
                        state = StateFlags.String;
                    }
                    else if (current == '-' && next == '-')
                    {
                        state = StateFlags.Comment;
                    }
                    else if (/*options.multilineComments &&*/ current == '/' && next == '*')
                    {
                        state = StateFlags.Comment | StateFlags.Multiline;
                        ++depth;
                        skipNext = true;
                    }
                    else
                    {
                        transition = false;
                    }

                    if (transition)
                    {
                        ParseToken(token, savedState, state, executionSql);
                        transition = false;
                        token.Clear();
                    }
                }
                else if (state.HasFlag(StateFlags.Comment))
                {
                    if (state.HasFlag(StateFlags.Multiline) && current == '*' && next == '/')
                    {
                        skipNext = true;
                        transition = --depth == 0;
                    }
                    else
                    {
                        transition = !state.HasFlag(StateFlags.Multiline) && current == '\n';
                    }

                    if (/*options.nestedMultlineComments &&*/ !transition && current == '/' && next == '*')
                    {
                        skipNext = true;
                        ++depth;
                    }
                }
                else if (state.HasFlag(StateFlags.String))
                {
                    if (current == '\'' && next == '\'' /*options.stringEscapeCharacter*/)
                    {
                        skipNext = true;
                        transition = false;
                    }
                    else
                    {
                        transition = /*(options.multilineStrings && current == '\n') ||*/  current == '\'';
                    }
                }

                token.Append(current);

                if (skipNext)
                {
                    token.Append(next);
                    ++i;
                }

                if (transition || next == null)
                {
                    ParseToken(token, savedState, null, executionSql);
                    token.Clear();

                    state = StateFlags.Literal;
                }
            }

            return executionSql;
        }

        private void ParseToken(StringBuilder buffer, StateFlags state, StateFlags? nextState, StringBuilder executionSql)
        {
            var token = buffer.ToString().Replace("\r", "");

            if (state.HasFlag(StateFlags.Comment)) {
                if (!state.HasFlag(StateFlags.Multiline))
                {
                    var match = _descriptionRegex.Match(token);

                    if (match.Success)
                    {
                        var name = match.Groups["name"].Value;
                        var parameter = Parameters.ContainsKey(name) ? Parameters[name] : new QueryParameter();

                        parameter.Label = match.Groups["label"].Value;

                        if (match.Groups["description"].Success)
                        {
                            parameter.Description = match.Groups["description"].Value;
                        }

                        Parameters[name] = parameter;
                    }

                    executionSql.Append('\n');
                }

                _sql.Append(token);
            }
            else
            {
                _sql.Append(token);

                if (!state.HasFlag(StateFlags.String))
                {
                    token = _whitespaceRegex.Replace(token, "${whitespace}");
                    token = _boundarySpacesRegex.Replace(token, "");

                    if (nextState.HasValue && nextState.Value.HasFlag(StateFlags.Comment) && !nextState.Value.HasFlag(StateFlags.Multiline))
                    {
                        token = token.TrimEnd(' ');
                    }
                }

                var matches = _paramsRegex.Matches(token);

                foreach (Match match in matches)
                {
                    var name = match.Groups["name"].Value;
                    var parameter = Parameters.ContainsKey(name) ? Parameters[name] : new QueryParameter();

                    if (match.Groups["type"].Success)
                    {
                        var type = match.Groups["type"].Value.ToLower();

                        if (CheckIfTypeIsKnown(type))
                        {
                            parameter.Type = type;
                        }
                        else
                        {
                            Errors.Add($"{name} has unknown parameter type {type}!");
                        }
                    }

                    if (match.Groups["default"].Success)
                    {
                        var value = match.Groups["default"].Value;

                        if (!parameter.Type.HasValue() || ValidateType(parameter.Type, value))
                        {
                            parameter.Default = value;
                        }
                        else
                        {
                            Errors.Add($"{name}'s default value of {value} is invalid for the type {parameter.Type}!");
                        }
                    }

                    parameter.Required = parameter.Default == null;

                    Parameters[name] = parameter;
                    token = token.ReplaceFirst(match.Value, "##" + name + "##");
                }

                if (executionSql.Length > 0 && executionSql[executionSql.Length - 1] == '\n')
                {
                    token = token.TrimStart('\n', ' ');
                }

                executionSql.Append(token);
            }
        }

        private string SubstituteParameters(StringBuilder sql, NameValueCollection requestParams)
        {
            foreach (var name in Parameters.Keys)
            {
                var parameter = Parameters[name];
                var value = requestParams != null && requestParams.Contains(name) ? requestParams[name] : parameter.Default;

                if (parameter.Required && !value.HasValue())
                {
                    Errors.Add($"Missing value for {name}!");

                    continue;
                }

                if (parameter.Type.HasValue())
                {
                    if (!ValidateType(parameter.Type, value))
                    {
                        Errors.Add($"Expected value of {name} to be a {parameter.Type}!");

                        continue;
                    }

                    value = EncodeType(parameter.Type, value);
                }

                sql.Replace("##" + name + "##", value);
            }

            return sql.ToString();
        }

        private static bool CheckIfTypeIsKnown(string type) => ParameterTypes.Any(p => p.TypeName == type);
        private static ParameterType GetType(string type) => ParameterTypes.First(p => p.TypeName == type);
        private static bool ValidateType(string type, string data) => GetType(type).Validator(data);
        private static string EncodeType(string type, string data) => GetType(type).Encoder(data);

        public struct QueryParameter
        {
            public string Type { get; set; }
            public string Default { get; set; }
            public string Value { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public bool Required { get; set; }
        }

        #region Nested type: ParameterType

        private class ParameterType
        {
            public Func<string, bool> Validator { get; }
            public Func<string, string> Encoder { get; }
            public string TypeName { get; }

            public ParameterType(string typeName, Func<string, bool> validator, Func<string, string> encoder)
            {
                TypeName = typeName;
                Encoder = encoder;
                Validator = validator;
            }
        }

        #endregion
    }
}