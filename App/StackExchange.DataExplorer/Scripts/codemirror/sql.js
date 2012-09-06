// See https://github.com/myape/CodeMirror2/blob/0b109319eda1e12f9d1a7024c7be640eda21ae36/mode/sql/sql.js
CodeMirror.defineMode("sql", function (config, parserConfig) {
    var indentUnit = config.indentUnit,
        keywords = parserConfig.keywords,
        functions = parserConfig.functions,
        types = parserConfig.types,
        operators = parserConfig.operators,
        multiLineStrings = parserConfig.multiLineStrings;
    var isOperatorChar = /[+\-*&%=<>!?:\/|]/;

    function chain(stream, state, f) {
        state.tokenize = f;
        return f(stream, state);
    }

    var type;
    function ret(tp, style) {
        type = tp;
        return style;
    }

    function tokenBase(stream, state) {
        var ch = stream.next();
        // start of string?
        if (ch == "'") {
            return chain(stream, state, tokenString(ch));
        }
        else if (ch == "[") {
            stream.skipTo("]");
            return "sql-word";
        }
        // is it one of the special signs {}().? 
        else if (/[{}\(\)\.]/.test(ch)) {
            return ret(ch);
        }
        // Seperator?
        else if (ch == "," || ch == ";") {
            return "sql-separator";
        }
        // start of a number value?
        else if (/\d/.test(ch)) {
            stream.eatWhile(/[\w\.]/)
            return ret("number", "sql-number");
        }
        // multi line comment or simple operator?
        else if (ch == "/") {
            if (stream.eat("*")) {
                return chain(stream, state, tokenComment);
            }
            else {
                stream.eatWhile(isOperatorChar);
                return ret("operator", "sql-operator");
            }
        }
        // single line comment or simple operator?
        else if (ch == "-") {
            if (stream.eat("-")) {
                stream.skipToEnd();
                return ret("comment", "sql-comment");
            } else {
                stream.eatWhile(isOperatorChar);
                return ret("operator", "sql-operator");
            }
        }
        // Data Explorer input variables
        else if (ch == '#') {
            if (stream.match(/^#([a-zA-Z][A-Za-z0-9]*)(?::([A-Za-z]+))?(?:\?([^#]+))?##/, true)) {
                return ret("word", "sql-special");
            } else {
                return ret("word", "sql-word");
            }
        }
        // sql variable?
        else if (ch == "@" || ch == "$") {
            stream.eatWhile(/[\w\d\$_]/);
            return ret("word", "sql-var");
        }
        // is it a operator?
        else if (isOperatorChar.test(ch)) {
            stream.eatWhile(isOperatorChar);
            return ret("operator", "sql-operator");
        }
        // a punctuation?
        else if (/[()]/.test(ch)) {
            return "sql-punctuation";
        } else {
            // get the whole word
            stream.eatWhile(/[\w\$_]/);
            // is it one of the listed keywords?
            if (keywords && keywords.propertyIsEnumerable(stream.current().toLowerCase())) return ret("keyword", "sql-keyword");
            // is it one of the listed functions?
            if (functions && functions.propertyIsEnumerable(stream.current().toLowerCase())) return ret("keyword", "sql-function");
            // is it one of the listed types?
            if (types && types.propertyIsEnumerable(stream.current().toLowerCase())) return ret("keyword", "sql-type");
            // is it one of the listed sqlplus keywords?
            if (operators && operators.propertyIsEnumerable(stream.current().toLowerCase())) return ret("keyword", "sql-operators");
            // default: just a "word"
            return ret("word", "sql-word");
        }

    }

    function tokenString(quote) {
        return function (stream, state) {
            var escaped = false, next, end = false;
            while ((next = stream.next()) != null) {
                // This isn't how T-SQL strings work, need to fix
                if (next == quote && !escaped) { end = true; break; }
                escaped = !escaped && next == "\\";
            }
            if (end || !(escaped || multiLineStrings))
                state.tokenize = tokenBase;
            var style = quote == "`" ? "sql-quoted-word" : "sql-literal";
            return ret("string", style);
        };
    }

    function tokenComment(stream, state) {
        var maybeEnd = false, ch;
        while (ch = stream.next()) {
            if (ch == "/" && maybeEnd) {
                state.tokenize = tokenBase;
                break;
            }
            maybeEnd = (ch == "*");
        }
        return ret("comment", "sql-comment");
    }

    // Interface

    return {
        startState: function (basecolumn) {
            return {
                tokenize: tokenBase,
                indented: 0,
                startOfLine: true
            };
        },

        token: function (stream, state) {
            if (stream.eatSpace()) return null;
            var style = state.tokenize(stream, state);
            return style;
        },
        electricChars: ")"

    };
});

(function () {
    function keywords(str) {
        var obj = {}, words = str.split(" ");
        for (var i = 0; i < words.length; ++i) obj[words[i]] = true;
        return obj;
    }
    var cKeywords =
        // http://msdn.microsoft.com/en-us/library/ms189822.aspx
        "add alter as asc authorization backup begin break browse " +
        "bulk by cascade case checkpoint close clustered coalesce " + 
        "colate column commit compute constraint contains " +
        "containstable continue convert create cross current cursor " +
        "database dbcc deallocate declare default delete deny desc " + 
        "disk distinct distributed drop dump else end escape " +
        "exec execute exit external fetch file filefactor for " +
        "foreign from function goto grant group having holdlock " +
        "if index insert into key kill lineno load merge national " +
        "nocheck nonclustered nocount off offsets on open opendatasource " + 
        "openquery openrowset openxml option order over percent " +
        "plan precision primary print proc procedure public read " +
        "readtext reconfigure reference replication restore restrict " +
        "return revert revoke rollback rowcount rowguidcol rule " +
        "save schema select set setuser shutdown statistics " +
        "table tablesample textsize then to top tran transaction " +
        "trigger truncate unique update updatetext use user values " +
        "varying view waitfor when where while with writetext";

    var cFunctions =
        // http://msdn.microsoft.com/en-us/library/ms173454.aspx
        "avg checksum_agg count count_big grouping grouping_id " +
        "max min sum stdev stdevp var varp " +
        // http://msdn.microsoft.com/en-us/library/ms186724.aspx
        "sysdatetime sysdatetimeoffset sysutcdatetime " +
        "current_timestamp getdate getutcdate datename" +
        "datepart day month year datediff dateadd switchoffset " +
        "todatetimeoffset isdate " +
        // http://msdn.microsoft.com/en-us/library/ms177516.aspx
        "abs acos asin atan atn2 ceiling cos cot degrees exp " +
        "floor log log10 pi power radians rand round sign sin " +
        "sqrt square tan" +
        // http://msdn.microsoft.com/en-us/library/ms189798.aspx
        "rank ntile dense_rank row_number " +
        // http://msdn.microsoft.com/en-us/library/ms181984.aspx
        "ascii char charindex difference left len lower ltrim " +
        "nchar patindex quotename replace replicate reverse right " +
        "rtrim soundex space str stuff substring unicode upper " +
        // http://msdn.microsoft.com/en-us/library/ms187786.aspx
        "cast convert isnull isnumeric nullif";

    var cTypes =
        // http://msdn.microsoft.com/en-us/library/ms187752.aspx
        "bigint bit decimal int money numeric smallint smallmoney " +
        "tinyint float real date datetime2 datetime datetimeoffset " +
        "smalldatetime time char text varchar nchar ntext nvarchar " +
        "binary image varbinary cursor hierarchyid sql_variant " +
        "timestamp uniqueidentifier xml";

    var cOperators =
        // http://msdn.microsoft.com/en-us/library/ms174986.aspx
        "~ ^ >= <= <> != !< !> += -= *= /= %= &= ^= |= all and any " +
        "between exists in like not or some except intersect union " +
        "join inner outer left right is null pivot unpivot";

    CodeMirror.defineMIME("text/x-t-sql", {
        name: "sql",
        keywords: keywords(cKeywords),
        functions: keywords(cFunctions),
        types: keywords(cTypes),
        operators: keywords(cOperators),
        multiLineStrings: true
    });
} ());