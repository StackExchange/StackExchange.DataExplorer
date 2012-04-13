DataExplorer.QueryEditor = (function () {
    var editor, field, params = {}, query,
        options = {
            'mode': 'text/x-t-sql'
        };

    function exists() {
        return !!editor || !!query;
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
                'extraKeys': {
                    'Ctrl-Enter': function () {
                        field.closest('form').submit();
                    }
                }
            }));
        } else {
            query = target[_textContent];
            editor = CodeMirror.runMode(query, options.mode, target);
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

        if (query) {
            return query;
        }   

        var value = editor.getValue();

        // Strip zero-width that randomly appears when copying text from the current
        // Data Explorer query editor into this one, at least until I can figure out
        // where it's coming from.
        if (value.charCodeAt(value.length - 1) === 8203) {
            value = value.substring(0, value.length - 1);
        }

        // Explicitly update the field, since CodeMirror might not have gotten a
        // chance to yet
        field.val(value);

        return value;
    }

    function parseParameters(sql) {
        // Until we fix this to handle the non-editor view too...
        var value = sql || getValue(),
            pattern = /##([a-zA-Z][A-Za-z0-9]*)(?::([A-Za-z]+))?(?:\?([^#]+))?##/,
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

                if (typeof params.items[param[1]].index === 'undefined') {
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
        'exists': exists,
        'parse': parseParameters
    };
})();

DataExplorer.TableHelpers = (function () {
    var tables,
        infoTemplate = document.create('span', {
            className: 'button table-data',
            text: 'show table',
            title: 'show the contents of this table'
        }),
        closeTemplate = document.create('span', {
            className: 'button table-data-close',
            text: 'close table',
            title: 'hide the contents of this table'
        }),
        dataTemplate = document.create('div', {
            className: 'table-data-panel hidden'
        });

    dataTemplate.appendChild(document.create('span', {
        className: 'schema-table'
    }));
    dataTemplate.appendChild((function () {
        var wrapper = document.create('div', {
                className: 'table-data-wrapper'
            }),
            table = document.createElement('table'),
            tableHead = document.createElement('thead'),
            tableBody = document.createElement('tbody');

        wrapper.appendChild(table);
        table.appendChild(tableHead);
        table.appendChild(tableBody);
        tableHead.appendChild(document.createElement('tr'));
        tableBody.appendChild(document.createElement('tr'));

        return wrapper;
    })());
    
    function init(tableData) {
        var shema = document.getElementById('schema');
        tables = tableData;

        $('ul .schema-table', schema).each(function () {
            var tableName = this[_textContent],
                data = tables[tableName];

            if (data) {
                var infoIcon = infoTemplate.cloneNode(true),
                    closeIcon = closeTemplate.cloneNode(true),
                    panel = dataTemplate.cloneNode(true), $panel = $(panel);

                this.appendChild(infoIcon);

                panel.children[0][_textContent] = tableName;
                panel.children[0].appendChild(closeIcon);

                $(closeIcon).click(function () {
                    $panel.hide();
                });

                var table = panel.getElementsByTagName('table')[0],
                    tableBody = table.children[1],
                    header = table.children[0].children[0],
                    record = tableBody.children[0];

                tableBody.removeChild(record);

                for (var i = 0; i < data.columns.length; ++i) {
                    header.appendChild(document.create('th', {
                        text: data.columns[i].name
                    }));
                    record.appendChild(document.create('td', {
                        className: data.columns[i].type.toLowerCase()
                    }));
                }

                for (var i = 0; i < data.rows.length; ++i) {
                    var row = record.cloneNode(true);

                    for (var c = 0; c < data.columns.length; ++c) {
                        row.children[c][_textContent] = data.rows[i][c];
                    }

                    tableBody.appendChild(row);
                }

                schema.insertBefore(panel, schema.children[0]);

                $(infoIcon).click(function () {
                    $panel.show();
                    return false;
                });
            }
        });
    }

    // This is terrible, because it requires the outside code to
    // account for the header height...will fix later
    function resize(height) {
        // Could store references directly to the wrapper, too...
        $('#schema .table-data-wrapper').css({ height: height + 'px' });
    }

    return {
        'init': init,
        'resize': resize
    };
})();

DataExplorer.ready(function () {
    var schema = $('#schema'),
        history = $('#history'),
        panel = $('#editor-panel'),
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
        var wrapper,
            toggle = $('#schema-toggle'),
            toolbar = $('#editor-toolbar'),
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

        function resizePanel(available) {
            var remaining = available - history.outerHeight(),
                list = schema.children('ul'),
                offset = schema.outerHeight() - list.height();

            list.height(remaining - offset);
            DataExplorer.TableHelpers.resize((available - offset) + 9);

            if (wrapper) {
                offset = wrapper.closest('.CodeMirror').outerHeight() - wrapper.height();
                
                wrapper.height(available - offset);
                editor.refresh();
            }
        }

        function resizeSchema() {
            var available = panel.outerHeight(),
                remaining = available - schema.outerHeight(),
                list = history.children('ul'),
                offset = history.outerHeight() - list.height();

            list.height(remaining - offset);
        }

        schema.TextAreaResizer(resizeSchema, {
            'offsetTop': schema.find('.heading').outerHeight(),
            'resizeSelector': 'ul'
        });
        schema.addClass('cm-s-' + editor.getOption('theme') + '');
        schema.delegate('.schema-table', 'click', function () {
            var self = $(this);

            self.next('dl').toggle();
        });
        schema.find('.expand').click(function () {
            schema.find('dl').show();
        });
        schema.find('.collapse').click(function () {
            schema.find('dl').hide();
        });

        // Set this resizer up after because the grippie adds height to the
        // sidebar that we need to factor in
        $('#editor').TextAreaResizer(resizePanel, { 
            'useParentWidth': true,
            'resizeWrapper': true,
            'minHeight': 300,
            'initCallback': true
        });

        function showSchema() {
            panel.add(toolbar).animate({ 'width': '70%' }, 'fast', function () {
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
                panel.add(toolbar).animate({ 'width': '100%' }, 'fast');
            } else {
                panel.add(toolbar).css('width', '100%');
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
        $('.report-option').hide();
        error.hide();

        var cleanup = function () {
            $('#loading').hide();

            form.find('input, button').prop('disabled', false);
        }

        var fail = function() {
            showError({ 'error': "Something unexpected went wrong while running "
                            + "your query. Don't worry, blame is already being assigned." });
        }

        var success = function(response) {
            if (response.running === true)
            {
                setTimeout(function(){
                       $.ajax({
                            'type': 'GET',
                            'url': '/query/job/' + response.job_id,
                            'success': success,
                            'error': [cleanup, fail],
                            'cache': false,
                        });  
                }, 1500);
            }
            else 
            {
                cleanup();
                parseQueryResponse(response);
            }
        }

        if (verifyParameters()) {
            var data = form.serialize();
            form.find('input, button').prop('disabled', true);
            
            $('#loading').show();
            
            $.ajax({
                'type': 'POST',
                'url': this.action,
                'data': data,
                'success': success,
                'error': [cleanup, fail],
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
        var defaultWidth = 958,
            availableWidth = document.documentElement.clientWidth - 100,
            grid = $('#resultSets'),
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

            if (field.value && field.value.length /*&& value != field.value*/) {
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
                if (DataExplorer.options.User.isAuthenticated &&
                        DataExplorer.options.User.guessedID) {
                    hasValue = true;
                    value = DataExplorer.options.User.guessedID;
                }
            }

            if (complete) {
                complete = hasValue;
            }

            field = document.createElement('input');
            field.name = ordered[i].name;
            field.id = 'dynParam' + i;
            field.type = 'text';

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
            params = $('#query-params input[type="text"]').serialize(),
            textOnly = false,
            userid;

        if (params) {
            params = params.replace(/(^|&)UserId=(\d+)(&|$)/i, function (match, g1, g2, g3) {
                userid = g2;

                return g1 ? g3 : "";
            });

            if (params) {
                params = '?' + params;
            }
        }

        if (/[^\d]\/\d+$/.test(action)) {
            form[0].action = action + '/' + response.querySetId;
        }

        if (response.resultSets.length) {
            results = response.resultSets[0];
            records = results.rows.length;
        } else {
            textOnly = true;
            response.resultSets = null;
        }

        document.getElementById('messages').children[0][_textContent] = response.messages;

        if (!slug && !/\/[^\/]+\/query\/new/.test(window.location.pathname) && /.*?\/[^\/]+$/.test(window.location.pathname)) {
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

        var target = "";
        if (response.targetSites == 1) { target = "all-"; } // all sites
        else if (response.targetSites == 2) { target = "all-meta-"; } // all meta sites
        else if (response.targetSites == 3) { target = "all-non-meta-"; } // all non meta sites

        DataExplorer.template('a.templated', 'href', {
            'targetsites': target,
            'site': response.siteName,
            'revisionid': response.revisionId,
            'slug': slug,
            'params': params,
            'id' : response.querySetId
        });

        if (userid) {
            userid = (params ? '&' : '?') + 'UserId=' + userid;

            var related = $('a.templated.related-site');

            if (related.length) {
                related[0].setAttribute('href', related[0].getAttribute('href') + userid);
            }
        }

        if (response.created) {
            var title = response.created.replace(/\.\d+Z/, 'Z'),
                href = "/" + response.siteName + "/revision/" + response.querySetId + "/" + response.revisionId + "/" + response.slug,
                classes = "selected";

            if (response.parentId) {
                history.find('#revision-' + response.parentId).addClass('parent');
            }

            history.find('.empty').remove();
            history.find('.selected').removeClass('selected');
            history.children('ul').prepend(
                '<li id="revision-' + response.revisionId + '" class="' + classes + '">' +
                    '<a href="' + href + '">' +
                        '<span class="revision-info">' + response.revisionId + '</span>' +
                    '</a>' +
                    '<span class="relativetime" title="' + title + '"></span>' +
                    '<div style="clear:both"></div>' +
                '</li>'
            );
            history.find('li:last').addClass('last');

            if (window.history && window.history.pushState && document.URL.indexOf("query/edit") == -1)
            {
                window.history.pushState(null,"", "/" + response.siteName + "/query/edit/" + response.querySetId);
            }
        }

        history.find('.relativetime').each(function () {
            this[_textContent] = Date.parseTimestamp(this.title).toRelativeTimeMini();
        });

        response.graph = !textOnly && isGraph(results);

        $('#query-results .miniTabs a.optional').each(function () {
            $(this).toggleClass('hidden', !response[this.href.from('#', false)]);
        });

        function selectFirst() {
            $('#query-results .miniTabs a:not(.hidden):first').click();
        }

        selectFirst();

        // We have to start showing the contents so that SlickGrid can figure
        // out the heights of its components correctly
        $('.result-option').fadeIn('fast').promise().done(function () {
            if (response.graph) {
                // Work-around for the div being display: none; originally
                $('#graphTab').click();
                renderGraph(results);
            }

            selectFirst();

            if (response.executionPlan && QP && typeof QP.drawLines === 'function') {
                $('#executionPlan').html(response.executionPlan);
            }

            if (!textOnly) {
                prepareTable($('#resultSets'), results, response);
                resizeResults();
            }
        
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
            error.hide();

            return false;
        }

        error.text(response.error).show();

        return true;
    }

    // Note that we destroy resultset in this function!
    function prepareTable(target, resultset, response) {
        var grid, 
            columns = resultset.columns, 
            rows = resultset.rows,
            row, 
            options, 
            hasTags = false, 
            widths = [], 
            variables = [],
            sizerParent = document.createElement('div'),
            sizer = document.createElement('span'),
            maxWidth = 290;

        sizerParent.className = 'offscreen ui-widget';
        sizerParent.appendChild(sizer);
        document.body.appendChild(sizerParent);

        for (var i = 0; i < rows.length; ++i) {
            row = {};

            for (var c = 0; c < columns.length; ++c) {
                row["col" + c] = rows[i][c];

                // Skip dates because we always know what length they'll be,
                // ignoring the case of the completely blank column
                if (columns[c].type === 'Date') {
                    continue;
                }

                if (rows[i][c] != null && i < 500 && (!widths[c] || widths[c] < maxWidth)) {
                    if (rows[i][c].toString().length < 30) {
                        sizer[_textContent] = rows[i][c];

                        if (sizer.offsetWidth > (widths[c] || 0)) {
                            widths[c] = sizer.offsetWidth;
                        }
                    } else {
                        widths[c] = maxWidth;
                    }
                }
            }
            rows[i] = row;
        }

        for (var i = 0; i < columns.length; ++i) {
            var name = columns[i].name.toLowerCase();

            if (columns[i].type === 'Date') {
                widths[i] = 160;
            }

            if (name === 'post link') {
                widths[i] = maxWidth;
            } else {
                sizer[_textContent] = columns[i].name;

                if (sizer.offsetWidth > widths[i]) {
                    widths[i] = sizer.offsetWidth;
                }
            }

            columns[i] = {
                'cssClass': columns[i].type === 'Number' ? 'number' : 'text',
                'id': "col" + i,
                'name': columns[i].name,
                'field': "col" + i,
                'type': columns[i].type.asVariable(),
                'width': Math.min((widths[i] || 50) + 16, maxWidth) 
            };

            if (name === 'tags' || name === 'tagname') {
                hasTags = true;
            }
        }

        document.body.removeChild(sizerParent);

        options = $.extend({}, gridOptions, {
            'formatterFactory': new ColumnFormatter(response),
            'rowHeight': hasTags ? 35 : 25, 
            'enableTextSelectionOnCells' : true
        });

        grid = new Slick.Grid(target, rows, columns, options);
        grid.onColumnsResized = resizeResults;
    }

    function ColumnFormatter(response) {
        var base = response.url,
            autolinker = /^(https?|site):\/\/[-A-Z0-9+&@#\/%?=~_\[\]\(\)!:,\.;]*[-A-Z0-9+&@#\/%=~_\[\]](?:\|.+?)?$/i,
            dummy = document.createElement('a'),
            wrapper = dummy,
            _outerHTML = 'outerHTML';

        if (!dummy.outerHTML) {
            wrapper = document.createElement('span');
            _outerHTML = 'innerHTML';
            wrapper.appendChild(dummy);
        }

        var siteColumnName = null;
        if(response.resultSets && response.resultSets[0])
        {
            var cols = response.resultSets[0].columns; 
            for (var i = 0; i < cols.length; i++) {
                if (cols[i]["type"] == "site")
                {
                    siteColumnName = "col" + i;
                }
            }
        }

        this.getFormatter = function (column) {
            
            if (column.name.toLowerCase() === 'tags' || column.name.toLowerCase() === 'tagName') {
                return tagFormatter(siteColumnName);
            } else if (column.type) {
                switch (column.type) {
                    case 'user':
                        return linkFormatter('/users/', siteColumnName);
                    case 'post':
                        return linkFormatter('/questions/', siteColumnName);
                    case 'suggestededit':
                        return linkFormatter('/suggested-edits/', siteColumnName);
                    case 'comment':
                        return linkFormatter('/posts/comments/', siteColumnName);
                    case 'date':
                        return dateFormatter;
                    case 'site':
                        return siteFormatter;
                }
            }

            return defaultFormatter;
        };

        function defaultFormatter(row, cell, value, column, context) {
            if (value == null) {
                value = "";
            }

            var matches;
            
            if (typeof value === 'string' && (matches = autolinker.exec(value))) {
                var url = value,
                    description = value,
                    split = value.split("|");

                if (split.length == 2) {
                    url = split[0];
                    description = split[1];
                }

                if (matches[1] === 'site') {
                    url = url.substring('site:/'.length);

                    if (siteColumnName) {
                        url = context[siteColumnName].url + url;
                    } else {
                        url = base + url;
                    }
                }

                dummy.setAttribute('href', url);
                // If we want literal entities to be rendered, this won't work
                // But I'm not sure why we would, so this seems reasonable.
                dummy[_textContent] = description;

                // Firefox doesn't have outerHTML, so we have some hackery...
                value = wrapper[_outerHTML];
            } else {
                value = encodeColumn(value);
            }

            return value;
        }
        
        function dateFormatter(row, cell, value, column, context) {
            if (!value) {
                return defaultFormatter(row, cell, value, column, context);
            }
            
            return (new Date(value)).toUTC();
        }

        function tagFormatter(siteColumnName) { 
            var siteColumnName = siteColumnName;

            return function(row, cell, value, column, context) {
                if (!value || value.search(/^(?:<[^<]+>)+$/) === -1) {
                    return defaultFormatter(row, cell, value, column, context);
                }

                var tags = value.substring(1, value.length - 1).split('><'),
                    template = '<a class="post-tag :class" href=":base/tags/:tag">:tag</a>',
                    value = '', tag;

                var url = base;
                if (siteColumnName != null)
                {
                    url = context[siteColumnName].url;
                }

                for (var i = 0; i < tags.length; ++i) {
                    tag = tags[i];

                    value = value + template.format({
                        'base': url,
                        'class': '',
                        'tag': tag
                    });
                }

                return value;
            }
        }

        function siteFormatter(row, cell, value, column, context) {
            var template = '<a href=":url">:text</a>';

            if (!value || typeof value !== 'object') {
                return defaultFormatter(row, cell, value, column, context);
            }

            return template.format({
                'url': value.url,
                'text': encodeColumn(value.name)
            });
        }


        function linkFormatter(path, siteColumnName) {
            var url = base + path, 
                template = '<a href=":url">:text</a>',
                siteColumnName = siteColumnName,
                path = path;

            return function (row, cell, value, column, context) {
                if (!value || typeof value !== 'object') {
                    return defaultFormatter(row, cell, value, column, context);
                }

                var currentUrl = url;

                if (siteColumnName != null)
                {
                    currentUrl = context[siteColumnName].url + path;
                }

                return template.format({
                    'url': currentUrl + value.id,
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
    if (!resultSet || resultSet.columns.length < 2) {
        return false;
    }

    var graph = true;

    for (var i = 0; i < resultSet.columns.length; i++) {
        var type = resultSet.columns[i]["type"];

        // allow for strings in the second column provided there are only 3 cols 
        if (type == 'Text' && i==1 && resultSet.columns.length == 3) {
            continue;
        }

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
        legend: { position: "nw" },
        grid: { hoverable: true },
        selection: { mode: "x" },
        series: { lines: { show: true }, points: { show: true} }
    };
    if (resultSet.columns[0]["type"] == 'Date') {
        options.xaxis = { mode: "time" };
    }
    var graph = $("#graph");

    var series = [];

    // if the second column is text we need to unpivot 
    if (resultSet.columns[1]["type"] == 'Text')
    {
        var columns = {}; 
        for (var row = 0; row < resultSet.rows.length; row++) {
            var columnLabel = resultSet.rows[row][1],
                columnName = "col_" + columnLabel;
            if (columns[columnName] === undefined)
            {
                columns[columnName] = (series.push({label: columnLabel, data: [] }) - 1);
            }
            series[columns[columnName]].data.push([resultSet.rows[row][0],  resultSet.rows[row][2]]);
        }
    }
    else 
    {
        for (var col = 1; col < resultSet.columns.length; col++) {
            series.push({label: resultSet.columns[col].name, data: []});
        }

        for (var row = 0; row < resultSet.rows.length; row++) {
            for (var col = 1; col < resultSet.columns.length; col++) {
                series[col - 1].data.push([resultSet.rows[row][0], resultSet.rows[row][col]]);
            }
        }
    }

    $.plot(graph, series, options);
    bindToolTip(graph, "");
}