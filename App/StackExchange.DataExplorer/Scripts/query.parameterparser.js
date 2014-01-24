DataExplorer.ParameterParser = (function () {
    var States = {
        Literal: 1 << 0,
        String: 1 << 1,
        Comment: 1 << 2,
        Multiline: 1 << 3
    };


    function parse(sql, options) {
        var results = [];

        var parameter = /##([a-zA-Z][A-Za-z0-9]*)(?::([A-Za-z]+))?(?:\?([^#]+))?##/,
            description = /-- *([a-zA-Z][A-Za-z0-9]*) *: *([^"]+)(?:"([^"]+)")?/,
            parameters = {};

        function parseToken(token, state) {
            if (!token || token == '\n') {
                return;
            }

            var match, selected;

            if (state & States.Comment) {
                if (!(state & States.Multiline)) {
                    if (match = token.match(description)) {
                        selected = parameters[match[1]];

                        if (!selected) {
                            parameters[match[1]] = selected = {};
                        }

                        selected.label = match[2].trim();

                        if (match[3]) {
                            selected.description = match[3].trim();
                        }
                    }
                }
            } else {
                while (match = token.match(parameter)) {
                    selected = parameters[match[1]];

                    if (!selected) {
                        parameters[match[1]] = selected = {};
                    }

                    if (typeof (selected.index) === 'undefined') {
                        selected.index = results.length;
                        results.push(selected);
                    }

                    if (!selected.name) {
                        selected.name = match[1];
                    }

                    if (match[2]) {
                        selected.type = match[2];
                    }

                    if (match[3]) {
                        selected.auto = match[3];
                    }

                    token = token.substring(match.index + match[0].length);
                }
            }
        }

        var state = States.Literal, token = '', depth = 0;
        var current, next, i = 0;

        while ((current = sql[i])) {
            next = sql[++i];

            var transition = true, savedState = state, skipNext = false;

            if (state & States.Literal) {
                if (current == "'") {
                    state = States.String;
                } else if (current + next == '--') {
                    state = States.Comment;
                } else if (options.multilineComments && current + next == '/*') {
                    state = States.Comment | States.Multiline;
                    ++depth;
                    skipNext = true;
                } else {
                    transition = false;
                }

                if (transition) {
                    parseToken(token, savedState);
                    transition = false;
                    token = '';
                }
            } else if (state & States.Comment) {
                if (state & States.Multiline && current + next == '*/') {
                    skipNext = true;
                    transition = !--depth;
                } else {
                    transition = !(state & States.Multiline) && current == '\n';
                }

                if (options.nestedMultilineComments && !transition && current + next == '/*') {
                    skipNext = true;
                    ++depth;
                }
            } else if (state & States.String) {
                if ((current + next == options.stringEscapeCharacter + "'")) {
                    skipNext = true;
                    transition = false;
                } else {
                    transition = (!options.multilineStrings && current == '\n') || current == "'";
                }
            }

            token += current;

            if (skipNext) {
                token += next;
                ++i;
            }

            if (transition || typeof (next) === 'undefined') {
                parseToken(token, savedState);
                token = '';
                state = States.Literal;
            }
        }

        return results;
    }

    return {
        parse: parse
    };
})();