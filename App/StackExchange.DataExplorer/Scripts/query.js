DataExplorer.QueryEditor = (function () {
    var editor, field, activeError,
        events = {
            'description': [],
            'title': []
        },
        metadata = {
            'title': null,
            'description': null
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
        editor = CodeMirror.fromTextArea(target[0], {
            'mode': 'text/x-t-sql',
            'lineNumbers': true,
            'onChange': onChange
        });

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
        parseMetadata();

        if (activeError !== null) {
            editor.setLineClass(activeError, null);

            activeError = null;
        }
    }

    function onError(line) {
        if (!DataExplorer.options.enableAdvancedSqlErrors || !editor) {
            return;
        }

        activeError = +line + parseMetadata();

        editor.setLineClass(activeError, 'error-line');
    }

    function parseMetadata() {
        // Determine the offset
        var offset = -1, lines = getValue().split("\n"), i,
            comments = false, title, description;

        for (i = 0; i < lines.length; ++i) {
            if (!comments && lines[i].indexOf('--') === 0) {
                lines[i] = lines[i].substring(2).trim();

                if (i === 0) {
                    title = lines[i];
                } else {
                    if (!description) {
                        description = "";
                    } else {
                        description = description + "\n";
                    }

                    description = description + lines[i];
                }

                offset++;
            } else if (/^\s*$/.test(lines[i])) {
                comments = true;
                offset++;
            } else {
                break;
            }
        }

        if (title !== metadata.title) {
            dispatch('title', metadata.title = title || "");
        }

        if (description !== metadata.description) {
            dispatch('description', metadata.description = description || "");
        }

        return offset;
    }

    return {
        'create': create,
        'value': getValue,
        'change': registerHandler,
        'error': onError,
        'exists': exists
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

        var data, sql = null;

        // Ugh, this is ugly, need to rework this soon
        if (DataExplorer.QueryEditor.exists()) {
            sql = DataExplorer.QueryEditor.value();
        } else {
            // This is a pretty big assumption
            sql = $('#queryBodyText').text();
        }

        if (sql && ensureAllParamsEntered(sql)) {
            $('#loading').show();
            //self.find('input').prop('disabled', true);

            $.ajax({
                'type': 'POST',
                'url': this.action,
                'data': form.serialize(),
                'success': parseQueryResponse,
                'error': function () {
                    $('#loading').hide();

                    showError({ 'error': "Something unexpected went wrong while running your query. Don't worry, blame is already being assigned." });
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

    function parseQueryResponse(response) {
        $('#loading').hide();

        if (showError(response)) {
            return;
        }

        if (response.captcha) {
            return displayCaptcha();
        }

        var action = form[0].action, records = 0,
            results, height = 0, maxHeight = 500,
            slug = response.slug;

        if (/.*?\/\d+\/\d+$/.test(action)) {
            action = action.substring(0, action.lastIndexOf('/'));
        }

        form[0].action = action + '/' + response.revisionId;

        if (response.resultSets.length) {
            results = response.resultSets[0];
            records = results.rows.length;
        } else {
            
        }

        if (!slug && /.*?\/[^\/]+$/.test(window.location.pathname)) {
            slug = window.location.pathname.substring(window.location.pathname.lastIndexOf('/'));
        } else if (slug && slug.indexOf('/') !== 0) {
            slug = '/' + slug;
        }

        DataExplorer.template('#result-stats span', 'text', {
            'records': records,
            'time': response.executionTime === 0 ? "<1" : response.executionTime,
            'cached': response.fromCache ? ' (cached)' : ''
        });

        DataExplorer.template('a.templated', 'href', {
            'multi': response.multiSite ? 'm' : '',
            'metas': response.excludeMetas ? 'n' : '',
            'site': response.siteName,
            'id': response.revisionId,
            'slug': slug
        });

        if (response.created) {
            var title = response.created.replace(/\.\d+Z/, 'Z'),
                href = "/" + response.siteName + "/query/edit/" + response.revisionId,
                classes = "selected";

            history.children('.selected').removeClass('selected');
            history.prepend('<li class="' + classes + '"><a href="' + href + '">' + response.revisionId + '<span class="relativetime" title="' + title + '"></span></a></li>');
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
    window.loadCachedResults = parseQueryResponse;

    function showError(response) {
        if (response && !response.error) {
            return false;
        }

        if (response.line) {
            DataExplorer.QueryEditor.error(response.line);
        }

        error.text(response.error).show();
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

function splitTags(s) {
    if (s == null) return [];
    var tmp = s.split("<");
    var rval = [];
    for (var i = 0; i < tmp.length; i++) {
        if (tmp[i] != "") {
            rval.push(tmp[i].replace(">", ""));
        }
    }

    return rval;
}

var current_results;

function scrollToResults() {

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
    $('#query-params').find("p input").each(function () {
        var val = getParameterByName(this.name);
        if (val != null && val.length > 0) {
            $(this).val(getParameterByName(this.name));
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

function ensureAllParamsEntered(query) {
    var pattern = /##([a-zA-Z0-9]+):?([a-zA-Z]+)?##/g;
    var params = query.match(pattern);
    if (params == null) params = [];

    var div = $('#query-params');

    var allParamsHaveValues = true;

    for (var i = 0; i < params.length; i++) {
        params[i] = params[i].substring(2, params[i].length - 2);
        var colonPos = params[i].indexOf(":");
        if (colonPos > 0) {
            params[i] = params[i].substring(0, colonPos); 
        }

        var currentParam = div.find("input[name=" + params[i] + "]");
        if (currentParam.length == 0) {
            div.append("<p><label>" + params[i] + "</label>\n" +
            "<input type=\"text\" name=\"" + params[i] + "\" value=\"\" /><div class='clear'></div></p>");
            allParamsHaveValues = false;
        } else {
            if (currentParam.val().length == 0) {
                allParamsHaveValues = false;
            }
        }
    }

    // remove extra params
    div.children("p").each(function () {
        var name = $(this).find('input').attr('name');
        var found = false;
        for (var i = 0; i < params.length; i++) {
            found = params[i] == name;
            if (found) break;
        }
        if (!found) {
            $(this).remove();
        } else {
            // auto param 
            if ($(this).find('input').val().length == 0 && name == "UserId") {
                $(this).find('input').val(DataExplorer.options.User.guessedID);
            }
        }
    });

    div.toggle(params.length > 0);

    if (params.length > 0 && !allParamsHaveValues) {
        div.find('input:first').focus();
    }

    return allParamsHaveValues;
}