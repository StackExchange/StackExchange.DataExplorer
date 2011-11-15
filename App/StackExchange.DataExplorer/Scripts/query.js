DataExplorer.QueryEditor = (function () {
    var editor, field, activeError, params = {},
        options = {
            'mode': 'text/x-t-sql'
        };

    function exists() {
        return !!editor;
    }

    function create(target, callback) {
        if (typeof target === 'string') {
            target = $(target);
        }

        if (!target.length) {
            return;
        }

        field = target;
        target = target[0];

        if (target.nodeName === 'TEXTAREA') {
            editor = CodeMirror.fromTextArea(target, $.extend({}, options, {
                'lineNumbers': true,
                'onChange': onChange
            }));
        } else {
            editor = CodeMirror.runMode(target[_textContent], options.mode, target);
        }

        if (callback && typeof callback === 'function') {
            callback(editor);
        }
    }

    function dispatch(event, value) {
        if (events[event]) {
            event = events[event];

            for (var i = 0; i < event.length; ++i) {
                event[i](value);
            }
        }
    }

    function getValue() {
        if (!exists()) {
            return null;
        }

        var value = editor.getValue();

        // Strip zero-width that randomly appears when copying text from the current
        // Data Explorer query editor into this one, at least until I can figure out
        // where it's coming from.
        if (value.charCodeAt(value.length - 1) === 8203) {
            value = value.substring(0, value.length - 1);

            // Explicitly update the field when this happens
            field.val(value);
        }

        return value;
    }

    function registerHandler(event, callback) {
        if (events[event] && typeof callback === 'function') {
            events[event].push(callback);
        }
    }

    function onChange() {
        if (activeError !== null) {
            editor.setLineClass(activeError, null);

            activeError = null;
        }
    }

    function onError(line) {
        if (!DataExplorer.options.enableAdvancedSqlErrors || !editor) {
            return;
        }

        activeError = +line;

        editor.setLineClass(activeError, 'error-line');
    }

    function parseParameters(sql) {
        // Until we fix this to handle the non-editor view too...
        var value = sql || getValue(),
            pattern = /##([a-zA-Z][A-Za-z0-9]*)(?::([^A-Za-z]+))?(?:\?([^#]+))?##/,
            commented = 0, stringified = false,
            params = { 'items': {}, 'count': 0 };

        if (!value || !value.length) {
            return;
        }

        value = value.split("\n");

        try {
            for (var i = 0; i < value.length; ++i) {
                parseLine(value[i]);
            }
        } catch (ex) {}

        return params;

        function parseLine(line, depth) {
            var line = line.trim(), param,
                endComment, endString,
                startComment, startString,
                substitutes = [];

            if (!line) {
                return;
            }

            if (typeof depth === 'undefined') {
                depth = 0;
            } else if (depth === 10) {
                throw new Error("The query body is too incomprehensible to continue "
                    + "parsing, or logic has failed us");
            } else {
                ++depth;
            }

            if (commented) {
                if ((endComment = line.indexOf('*/')) !== -1) {
                    commented += line.substring(0, endComment).split('/*').length - 1;
                    line = line.substring(endComment + '*/'.length);

                    if (--commented) {
                        return parseLine(line, depth);
                    }
                }
            } else if (stringified) {
                if ((endString = line.indexOf("'")) === -1) {
                    return scan(line);
                }

                scan(line.substring(0, endString));
                line = line.substring(endString + 1);

                if (line[0] === "'") {
                    return parseLine(line.substring(1), depth);
                }

                stringified = false;
            }

            line = line.replace("''", '');
            line = line.replace(/'[^']+'/, function (match) {
                substitutes.push(match);

                return '~S' + (substitutes.length - 1);
            });
            line = line.split('--');
            startString = line[0].indexOf("'");
            startComment = line[0].indexOf('/*');

            if (startString !== -1 || startComment !== -1) {
                line = line.join('--');

                if ((startString < startComment && startString !== -1) ||
                        startComment === -1) {
                    stringified = true;
                } else {
                    ++commented;
                    parseLine(line.substring(startComment + '/*'.length), depth);
                    line = line.substring(0, startComment);
                }
            } else if (commented) {
                return;
            } else {
                line = line[0];
            }

            for (var i = 0; i < substitutes.length; ++i) {
                line = line.replace('~S' + i, substitutes[i]);
            }

            scan(line);
        }

        function scan(segment) {
            while (param = segment.match(pattern)) {
                params.items[param[1]] = params.items[param[1]] || {};

                if (!params.items[param[1]].index) {
                    params.items[param[1]].index = params.count;
                    ++params.count;
                }

                params.items[param[1]].type = param[2] || params.items[param[1]].type;
                params.items[param[1]].auto = param[3] || params.items[param[1]].auto;

                segment = segment.substring(param.index + param[0].length);
            }
        }
    }

    return {
        'create': create,
        'value': getValue,
        'change': registerHandler,
        'error': onError,
        'exists': exists,
        'parse': parseParameters
    };
})();

DataExplorer.ready(function () {
    var schema = $('#schema'),
        history = $('#history'),
        panel = $('#query .left-group'),
        metadata = $('#query-metadata .info'),
        gridOptions = {
            'enableCellNavigation': false,
            'enableColumnReorder': false,
            'enableCellRangeSelection': false
        },
        error = $('#error-message'),
        form = $('#runQueryForm');

    DataExplorer.QueryEditor.create('#queryBodyText');
    DataExplorer.QueryEditor.create('#sql', function (editor) {
        var wrapper, resizer, border = 2,
            toggle = $('#schema-toggle'),
            schemaPreference = null;

        if (DataExplorer.options.User.isAuthenticated) {
            schemaPreference = new DataExplorer.DeferredRequest({
                'url': '/users/save-preference/:id/HideSchema'.format({
                    'id': DataExplorer.options.User.id
                })
            });
        }

        if (editor) {
            wrapper = $(editor.getScrollerElement());
        }

        function resize () {
            var available = resizer.height(),
                remaining = available - history.outerHeight(true);

            schema.height(remaining);

            if (wrapper) {
                wrapper.height(available - 10);
                editor.refresh();
            }
        }

        resizer = $('#wrapper').TextAreaResizer(resize);
        resize();
        
        schema.addClass('cm-s-' + editor.getOption('theme') + '');
        schema.delegate('.schema-table', 'click', function () {
            var self = $(this);

            self.toggleClass('closed');
            self.next('dl').toggle();
        });
        schema.find('.schema-table').click();

        function showSchema() {
            panel.animate({ 'width': '70%' }, 'fast', function () {
                schema.show();
                history.show();
            });
            toggle.text("hide sidebar").removeClass('hidden');

            if (schemaPreference) {
                schemaPreference.request({ 'value': false });
            }
        }

        function hideSchema(immediately) {
            schema.hide();
            history.hide();
            
            if (immediately !== true) {
                panel.animate({ 'width': '100%' }, 'fast');
            } else {
                panel.css('width', '100%');
            }

            toggle.text("show sidebar").addClass('hidden');

            if (schemaPreference) {
                schemaPreference.request({ 'value': true });
            }
        }

        if (DataExplorer.options.User.hideSchema) {
            hideSchema(true);
        }

        toggle.click(function () {
            var self = $(this);

            if (self.hasClass('hidden')) {
                showSchema();
            } else {
                hideSchema();
            }
        });
    });

    $('.miniTabs').tabs();

    form.submit(function () {
        $('.report-option').fadeOut();
        error.fadeOut();

        if (verifyParameters()) {
            form.find('input, button').prop('disabled', true);
            
            $('#loading').show();
            
            $.ajax({
                'type': 'POST',
                'url': this.action,
                'data': form.serialize(),
                'success': parseQueryResponse,
                'error': function () {
                    showError({ 'error': "Something unexpected went wrong while running "
                        + "your query. Don't worry, blame is already being assigned." });
                },
                'complete': function () {
                    $('#loading').hide();

                    form.find('input, button').prop('disabled', false);
                },
                'cache': false,
            });
        }

        return false;
    });

    $('#query-results').bind('show', function (event) {
        $('.download-button', this).hide();
        $(event.target.href.from('#') + 'Button').show();
    });

    $('#executionPlanTab').click(function () {
        QP.drawLines();
    });
    $(window).resize(resizeResults);

    function resizeResults() {
        var defaultWidth = 950,
            availableWidth = document.documentElement.clientWidth - 100,
            grid = $('#resultset'),
            gridWidth = grid.outerWidth(),
            canvas = grid.find('.grid-canvas'),
            canvasWidth = canvas.outerWidth(),
            width = 0;

        if (canvasWidth < defaultWidth || availableWidth < defaultWidth) {
            grid.width(width = defaultWidth);
        } else if (canvasWidth > availableWidth) {
            grid.width(width = availableWidth);
        } else {
            grid.width(width = canvasWidth);
        }

        if (width === defaultWidth) {
            grid.css('left', '0px');
        } else {
            grid.css('left', '-' + Math.round((width - defaultWidth) / 2) + 'px');
        }
    }

    // Ideally we can separate out the actual displaying bits so that the user
    // doesn't have to click the button before getting the form.
    function verifyParameters() {
        var sql = null;
        
        // Ugh, this is ugly, need to rework this soon
        if (DataExplorer.QueryEditor.exists()) {
            sql = DataExplorer.QueryEditor.value();
        } else {
            // This is a pretty big assumption
            sql = $('#queryBodyText').text();
        }

        if (!sql) {
            return false;
        }

        var params = DataExplorer.QueryEditor.parse(sql),
            ordered = [],
            complete = true,
            wrapper = document.getElementById('query-params'),
            fieldList = wrapper.getElementsByTagName('input'),
            fields = {},
            field, name, label, row, value, hasValue, key, first;

        $(wrapper).toggle(!!params.count);

        for (var i = fieldList.length - 1; i > -1 ; --i) {
            field = fieldList.item(i);
            value = field.getAttribute('value');
            name = field.name;

            if (value && value.length && value != field.value) {
                fields[name] = field.value; 
            }

            field.parentNode.parentNode.removeChild(field.parentNode);
        }

        for (key in params.items) {
            ordered[params.items[key].index] = params.items[key];
            ordered[params.items[key].index].name = key;
        }

        for (var i = 0; i < ordered.length; ++i) {
            label = document.createElement('label');
            label.htmlFor = 'dynParam' + i;
            label[_textContent] = ordered[i].name;

            value = fields[ordered[i].name];
            hasValue = !(!value && value !== 0);

            if (!hasValue) {
                value = window.location.param(ordered[i].name);
                hasValue = !(!value && value !== 0);
            }

            if (!hasValue) {
                value = ordered[i].auto;
                hasValue = !(!value && value !== 0);
            }

            if (!hasValue && ordered[i].name.toLowerCase() === 'userid') {
                if (DataExplorer.options.User.isAuthenticated) {
                    hasValue = true;
                    value = DataExplorer.options.User.guessedID;
                }
            }

            if (complete) {
                complete = hasValue;
            }

            field = document.createElement('input');
            field.name = ordered[i].name;

            if (hasValue) {
                field.setAttribute('value', value);
            } else if (!first) {
                first = field;
            }

            row = document.createElement('div');
            row.className = 'form-row';
            row.appendChild(label);
            row.appendChild(field);

            wrapper.appendChild(row);
        }

        if (!complete && first) {
            first.focus();
        }

        return complete;
    }

    function parseQueryResponse(response) {
        if (showError(response)) {
            return;
        }

        if (response.captcha) {
            return displayCaptcha();
        }

        var action = form[0].action, records = 0,
            results, height = 0, maxHeight = 500,
            slug = response.slug,
            params = $('#query-params input[type="text"]').serialize();

        if (params) {
            params = '?' + params;
        }

        if (/.*?\/\d+\/\d+$/.test(action)) {
            action = action.substring(0, action.lastIndexOf('/'));
        }

        form[0].action = action + '/' + response.revisionId;

        if (response.resultSets.length) {
            results = response.resultSets[0];
            records = results.rows.length;
        } else {
            
        }

        document.getElementById('messages').children[0][_textContent] = response.messages;

        if (!slug && /.*?\/[^\/]+$/.test(window.location.pathname)) {
            slug = window.location.pathname.substring(window.location.pathname.lastIndexOf('/'));

            if (/\d+/.test(slug)) {
                slug = null;
            }
        } else if (slug && slug.indexOf('/') !== 0) {
            slug = '/' + slug;
        }

        DataExplorer.template('#execution-stats', 'text', {
            'records': records,
            'time': response.executionTime === 0 ? "<1" : response.executionTime,
            'cached': response.fromCache ? ' (cached)' : ''
        });

        DataExplorer.template('a.templated', 'href', {
            'multi': response.multiSite ? 'm' : '',
            'metas': response.excludeMetas ? 'n' : '',
            'site': response.siteName,
            'id': response.revisionId,
            'slug': slug,
            'params': params
        });

        if (response.created) {
            var title = response.created.replace(/\.\d+Z/, 'Z'),
                href = "/" + response.siteName + "/query/edit/" + response.revisionId,
                classes = "selected";

            if (response.parentId) {
                history.find('#revision-' + response.parentId).addClass('parent');
            }

            history.find('.selected').removeClass('selected');
            history.children('ul').prepend(
                '<li id="revision-' + response.revisionId + '" class="' + classes + '">' +
                    '<a href="' + href + '">' +
                        '<span class="revision-info">' + response.revisionId + '</span>' +
                        '<span class="relativetime" title="' + title + '"></span>' + 
                    '</a>' +
                '</li>'
            );
        }

        history.find('.relativetime').each(function () {
            this[_textContent] = Date.parseTimestamp(this.title).toRelativeTimeMini();
        });

        response.graph = isGraph(results);

        $('#query-results .miniTabs a.optional').each(function () {
            $(this).toggle(!!response[this.href.from('#', false)]);
        });

        $('#query-results .miniTabs a:first').click();

        // We have to start showing the contents so that SlickGrid can figure
        // out the heights of its components correctly
        $('.result-option').fadeIn('fast').promise().done(function () {
            if (response.graph) {
                // Work-around for the div being display: none; originally
                $('#graphTab').click();
                renderGraph(results);
            }

            $('#query-results .miniTabs a:first').click();

            if (response.executionPlan && QP && typeof QP.drawLines === 'function') {
                $('#executionPlan').html(response.executionPlan);
            }

            prepareTable($('#resultset'), results, response);
            resizeResults();
        
            // Currently this always gives us 500 because it's what #resultset has
            // set in CSS. SlickGrid needs the explicit height to render correctly
            // though, so once we figure out how to resize #resultset dynamically
            // then this will be a bit more useful.
            $('#query-results .panel').each(function () {
                var currentHeight = $(this).height();

                if (currentHeight >= maxHeight) {
                    height = maxHeight;
                    return false;
                }

                height = Math.max(currentHeight, height);
            }).animate({ 'height': Math.min(height, maxHeight) });

            $('html, body').animate({
                scrollTop: $("#query-results").offset().top - 10
            }, 500);
        });
    }

    // Temporary workaround
    window.loadCachedResults = function (cache) {
        verifyParameters();

        if (cache) {
            parseQueryResponse(cache);
        }
    }

    function showError(response) {
        if (response && !response.error) {
            return false;
        }

        if (response.line) {
            DataExplorer.QueryEditor.error(response.line);
        }

        error.text(response.error).show();

        return true;
    }

    // Note that we destroy resultset in this function!
    function prepareTable(target, resultset, response) {
        var grid, columns = resultset.columns, rows = resultset.rows,
            row, options, hasTags = false, widths = [];

        for (var i = 0; i < rows.length; ++i) {
            row = {};

            for (var c = 0; c < columns.length; ++c) {
                row[columns[c].name.asVariable()] = rows[i][c];

                if (rows[i][c] && (!widths[c] || rows[i][c].length > widths[c])) {
                    widths[c] = rows[i][c].length;
                }
            }

            rows[i] = row;
        }

        for (var i = 0; i < columns.length; ++i) {
            if (columns[i].type === 'Date') {
                widths[i] = 14;
            }

            columns[i] = {
                'cssClass': columns[i].type === 'Number' ? 'number' : 'text',
                'id': columns[i].name.asVariable(),
                'name': columns[i].name,
                'field': columns[i].name.asVariable(),
                'type': columns[i].type.asVariable(),
                'width': Math.min(widths[i] || 5, 25) * 12 
            };

            if (columns[i].field === 'tags' || columns[i].field === 'tagName') {
                hasTags = true;
            }
        }

        options = $.extend({}, gridOptions, {
            'formatterFactory': new ColumnFormatter(response.url),
            'rowHeight': hasTags ? 35 : 25
        });

        grid = new Slick.Grid(target, rows, columns, options);
        grid.onColumnsResized = resizeResults;
    }

    function ColumnFormatter(base) {
        var base = base;

        this.getFormatter = function (column) {
            if (column.field === 'tags' || column.field == 'tagName') {
                return tagFormatter;
            } else if (column.type) {
                switch (column.type) {
                    case 'user':
                        return linkFormatter('/users/');
                    case 'post':
                        return linkFormatter('/questions/');
                    case 'suggestedEdits':
                        return linkFormatter('/suggested-edits/');
                    case 'date':
                        return dateFormatter;
                }
            }

            return defaultFormatter;
        };

        function defaultFormatter(row, cell, value, column, context) {
            return value ? encodeColumn(value) : "";
        }
        
        function dateFormatter(row, cell, value, column, context) {
            if (!value) {
                return defaultFormatter(row, cell, value, column, context);
            }
            
            return (new Date(value)).toString("yyyy-MM-dd HH:mm:ss");
        }

        function tagFormatter(row, cell, value, column, context) {
            if (!value || value.search(/^(?:<[^<]+>)+$/) === -1) {
                return defaultFormatter(row, cell, value, column, context);
            }

            var tags = value.substring(1, value.length - 1).split('><'),
                template = '<a class="post-tag :class" href=":base/tags/:tag">:tag</a>',
                value = '', tag;

            for (var i = 0; i < tags.length; ++i) {
                tag = tags[i];

                value = value + template.format({
                    'base': base,
                    'class': '',
                    'tag': tag
                });
            }

            return value;
        }

        function linkFormatter(path) {
            var url = base + path, template = '<a href=":url">:text</a>';

            return function (row, cell, value, column, context) {
                if (!value || typeof value !== 'object') {
                    return defaultFormatter(row, cell, value, column, context);
                }

                return template.format({
                    'url': url + value.id,
                    'text': encodeColumn(value.title)
                });
            };
        }
    }
});

function encodeColumn(s) {
    if (s != null && s.replace != null) {
        s = s.replace(/[\n\r]/g, " ")
              .replace(/&(?!\w+([;\s]|$))/g, "&amp;")
              .replace(/</g, "&lt;")
              .replace(/>/g, "&gt;")
              .substring(0, 400);
        return s;
    } else {
        return s;
    }
}

// this is from SO 901115
function getParameterByName(name) {
    name = name.replace(/[\[]/, "\\\[").replace(/[\]]/, "\\\]");
    var regexS = "[\\?&]" + name + "=([^&#]*)";
    var regex = new RegExp(regexS);
    var results = regex.exec(window.location.href);
    if (results == null)
        return "";
    else
        return decodeURIComponent(results[1].replace(/\+/g, " "));
}

function populateParamsFromUrl() {
    $('#query-params input').each(function () {
        var value = getParameterByName(this.name);

        if (value != null && value.length > 0) {
            this.value = value;
        }
    });
}

function displayCaptcha() {
    $('form input[type=submit]').hide();
    $('#captcha').show();

    $("#recaptcha_response_field").keydown(function (key) {
        if (key.keyCode == 13) {
            $("#btn-captcha").click();
            return false;
        }
        return true;
    }).focus();

    var captcha = function () {
        $(this).unbind("click");
        $.ajax({
            url: '/captcha',
            data: $('#captcha').closest('form').serialize(),
            type: 'POST',
            success: function (data) {
                if (data.success) {
                    $('form input[type=submit]').show();
                    $('#captcha').hide();
                    $('#captcha').closest('form').submit();
                } else {
                    $("#captcha-error").fadeIn();
                    $("#btn-captcha").click(captcha);
                    var text = $("#recaptcha_response_field");
                    var once = function () { $("#captcha-error").fadeOut(); text.unbind("keydown", once) };
                    text.keydown(once);
                }
            },
            dataType: "json"
        });

    };

    $("#btn-captcha").click(captcha);
}

function isGraph(resultSet) {

    var graph = true;

    for (var i = 0; i < resultSet.columns.length; i++) {
        var type = resultSet.columns[i]["type"];
        if (i != 0 && type == 'Date') {
            graph = false;
            break;
        }
        if (type != 'Number' && type != 'Date') {
            graph = false;
            break;
        } 
    }
    return graph;
}

function showTooltip(x, y, contents) {
    $('<div id="tooltip">' + contents + '<\/div>').css({
        position: 'absolute',
        display: 'none',
        top: y + 5,
        left: x + 5,
        border: '1px solid #fdd',
        padding: '2px',
        'background-color': '#fee',
        opacity: 0.80
    }).appendTo("body").fadeIn(200);
}

function addCommas(nStr) {
    nStr += '';
    x = nStr.split('.');
    x1 = x[0];
    x2 = x.length > 1 ? '.' + x[1] : '';
    var rgx = /(\d+)(\d{3})/;
    while (rgx.test(x1)) {
        x1 = x1.replace(rgx, '$1' + ',' + '$2');
    }
    return x1 + x2;
}

function bindToolTip(graph, suffix) {
    var previousPoint = null;
    var lastCall = 0;
    graph.bind("plothover", function (event, pos, item) {

        var toolTip;

        if (item) {

            if (previousPoint == null || previousPoint[0] != item.datapoint[0] || previousPoint[1] != item.datapoint[1]) {
                previousPoint = item.datapoint;

                $("#tooltip").remove();
                var x = item.datapoint[0].toFixed(2),
                    y = item.datapoint[1].toFixed(2);

                showTooltip(item.pageX - 10, item.pageY - 40, addCommas(parseInt(y)) + suffix);
            }
        }
        else {
            if (previousPoint != null) {
                $("#tooltip").remove();
            }
            previousPoint = null;
        }

    });
}

function renderGraph(resultSet) {

    var options = {
        legend: { position: "ne" },
        grid: { hoverable: true },
        selection: { mode: "x" },
        series: { lines: { show: true }, points: { show: true} }
    };
    if (resultSet.columns[0]["type"] == 'Date') {
        options.xaxis = { mode: "time" };
    }
    var graph = $("#graph");

    var series = [];

    for (var col = 1; col < resultSet.columns.length; col++) {
        series.push({label: resultSet.columns[col].name, data: []});
    }


    for (var col = 1; col < resultSet.columns.length; col++) {
        series.push([]);
    }

    for (var row = 0; row < resultSet.rows.length; row++) {
        for (var col = 1; col < resultSet.columns.length; col++) {
            series[col - 1].data.push([resultSet.rows[row][0], resultSet.rows[row][col]]);
        }
    }

    $.plot(graph, series, options);
    bindToolTip(graph, "");
}