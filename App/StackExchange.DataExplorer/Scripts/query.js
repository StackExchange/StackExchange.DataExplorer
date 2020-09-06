DataExplorer.QueryEditor = (function () {
    var editor, field, query,
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
            function run() {
                field.closest('form').submit();
            }

            editor = CodeMirror.fromTextArea(target, $.extend({}, options, {
                'lineNumbers': true,
                'extraKeys': {
                    'Ctrl-Enter': run,
                    'Shift-Tab': 'indentLess',
                    'Tab': 'indentMore',
                    'F5': run
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

    return {
        'create': create,
        'value': getValue,
        'exists': exists
    };
})();

DataExplorer.ready(function () {
    var schema = $('#schema'),
        history = $('#history'),
        panel = $('#editor-panel'),
        metadata = $('#query-metadata .info'),
        error = $('#error-message'),
        form = $('#runQueryForm');

    DataExplorer.QueryEditor.create('#queryBodyText');
    DataExplorer.QueryEditor.create('#sql', function (editor) {
        var wrapper;

        DataExplorer.Sidebar.init({
            editorTheme: editor.getOption('theme'),
            panel: panel,
            toolbar: '#editor-toolbar'
        });

        if (editor) {
            wrapper = $(editor.getScrollerElement()).closest('.CodeMirror');
        }

        function resizePanel(available) {
            var remaining = available - history.outerHeight(),
                list = schema.children('ul'),
                offset = schema.outerHeight() - list.height();

            list.height(remaining - offset);
            DataExplorer.TableHelpers.resize((available - offset) + 9);

            if (wrapper) {
                offset = wrapper.outerHeight() - wrapper.height();
                
                wrapper.height(available - offset);
                editor.refresh();
            }
        }

        // Set this resizer up after because the grippie adds height to the
        // sidebar that we need to factor in
        $('#editor').TextAreaResizer(resizePanel, { 
            'useParentWidth': true,
            'resizeWrapper': true,
            'minHeight': 300,
            'initCallback': true
        });
    });

    $('.miniTabs').tabs(false);

    form.submit(function () {
        $('.report-option').hide();
        error.hide();

        var cleanup = function () {
            $('#loading').hide();

            form.find('input, button').prop('disabled', function () {
                return this.id == 'cancel-query';
            });
        };

        var fail = function() {
            showError({ 'error': "Something unexpected went wrong while running "
                            + "your query. Don't worry, blame is already being assigned." });
        };

        var cancel = function () {
            showError({ 'error': 'Query execution has been cancelled' }, 'notice');
        }

        var pending = { request: null, timeout: null, setupCancel: true };
        var success = function(response) {
            if (response.running === true)
            {
                var poll = function () {
                    pending.timeout = setTimeout(function(){
                        pending.request = $.ajax({
                            'type': 'GET',
                            'url': '/query/job/' + response.job_id,
                            'success': success,
                            'error': [cleanup, fail],
                            'cache': false
                        });  
                    }, 1500);
                };

                if (!pending.timeout && pending.setupCancel) {
                    var job = response.job_id;
                    pending.setupCancel = false;

                    $('#cancel-query').one('click', function () {
                        this.disabled = true;

                        clearTimeout(pending.timeout);

                        if (pending.request) {
                            pending.request.abort();
                        }

                        $.ajax({
                            type: 'POST',
                            url:  '/query/job/' + job + '/cancel',
                            success: function (response) {
                                if (response.cancelled) {
                                    cleanup();
                                    cancel();
                                } else {
                                    // There were some results, so we're going to try and get whatever
                                    // was being returned when the user decided to cancel
                                    poll();
                                }
                            },
                            error: [cleanup, fail],
                            cache: false
                        });
                    }).prop('disabled', false);
                }

                poll();
            }
            else 
            {
                cleanup();
                parseQueryResponse(response);
            }
        };

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

    $('#query-options').find('input, select').each(function () {
        var value = window.location.param('opt.' + this.name);

        if (value) {
            if (this.type === 'checkbox') {
                this.checked = value;
            } else {
                this.value = value;
            }
        }
    });

    $('#query-results').bind('show', function (event) {
        $('.download-button', this).hide();
        $(event.target.href.from('#') + 'Button').show();
    });

    $('#executionPlanTab').click(function () {
        QP.drawLines();
    });

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

        var parameters = new DataExplorer.ParameterParser.parse(sql, {
            multilineStrings: true,
            multilineComments: true,
            stringEscapeCharacter: "'",
            nestedMultilineComments: true
        });

        var complete = true,
            wrapper = document.getElementById('query-params'),
            fieldList = wrapper.getElementsByTagName('input'),
            fields = {},
            field, name, label, row, value, hasValue, key, first;

        $(wrapper).toggle(!!parameters.length);

        for (var i = fieldList.length - 1; i > -1 ; --i) {
            field = fieldList.item(i);
            value = field.getAttribute('value');
            name = field.name;

            if (field.value && field.value.length /*&& value != field.value*/) {
                fields[name] = field.value; 
            }

            field.parentNode.parentNode.removeChild(field.parentNode);
        }

        for (var i = 0; i < parameters.length; ++i) {
            label = document.createElement('label');
            label.htmlFor = 'dynParam' + i;
            label[_textContent] = parameters[i].label || parameters[i].name;
            
            if (parameters[i].description) {
                label.title = parameters[i].description;
            }

            value = fields[parameters[i].name];
            hasValue = !(!value && value !== 0);

            if (!hasValue) {
                value = window.location.param(parameters[i].name);
                hasValue = !(!value && value !== 0);
            }

            if (!hasValue) {
                value = parameters[i].auto;
                hasValue = !(!value && value !== 0);
            }

            if (!hasValue && parameters[i].name.toLowerCase() === 'userid') {
                if (DataExplorer.options.User.isAuthenticated && DataExplorer.options.User.guessedID) {
                    hasValue = true;
                    value = DataExplorer.options.User.guessedID;
                }
            }

            if (complete) {
                complete = hasValue;
            }

            field = document.createElement('input');
            field.name = parameters[i].name;
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
        if (showError(response) || showCaptcha(response)) {
            return;
        }

        var action = form[0].action,
            records = 0,
            results,
            slug = response.slug,
            params = $('#query-params input[type="text"]').serialize(),
            textOnly = false,
            userid;

        if (params) {
            params = params.replace(/(^|&)UserId=(\d+)(&|$)/i, function (match, g1, g2, g3) {
                userid = g2;

                return g1 ? g3 : "";
            });

            if (params.length) {
                params = '?' + params;
            } else {
                params = null;
            }
        }

        if (/[^\d]\/\d+$/.test(action)) {
            form[0].action = action + '/' + response.querySetId;
        }

        if (response.resultSets.length) {
            response.resultSets.forEach(function (resultSet) {
                records += resultSet.rows.length;
            });
        } else {
            textOnly = true;
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
            'records': textOnly ? "Results" : records + " rows",
            'time': response.executionTime === 0 ? "<1" : response.executionTime,
            'cached': response.fromCache ? ' (cached)' : ''
        });

        var target = "";
        if (response.targetSites == 1) { target = "all-"; } // all sites
        else if (response.targetSites == 2) { target = "all-meta-"; } // all meta sites
        else if (response.targetSites == 3) { target = "all-non-meta-"; } // all non meta sites
        else if (response.targetSites == 4) { target = "all-meta-but-mse-"; } // all meta sites except mse
        else if (response.targetSites == 5) { target = "all-non-meta-but-so-"; } // all non meta sites except so
        
        var options;

        $('#query-options').find('input, select').each(function () {
            var value;

            if (this.type === 'checkbox') {
                if (this.checked) {
                    value = true;
                }
            } else {
                value = this.value;

                // If this is a select and the selected option is set in the HTML as the default one,
                // there's no sense in appending it to the query string
                if (this.tagName == 'SELECT' && this.options[this.selectedIndex].getAttribute('selected') !== null) {
                    value = null;
                }
            }

            if (value) {
                options = (options ? options + '&' : '') + 'opt.' + this.name + '=' + encodeURIComponent(value);
            }
        });

        if (options) {
            params = (params ? params + '&' : '?') + options;
        }

        var formatOptions = {
            'targetsites': target,
            'site': response.siteName,
            'revisionid': response.revisionId,
            'slug': slug,
            'params': params,
            'id' : response.querySetId
        };

        DataExplorer.template('a.templated.site', 'href', formatOptions);
        DataExplorer.SiteSwitcher.update(formatOptions);

        if (userid) {
            formatOptions.params = (params ? params + '&' : '?') + 'UserId=' + userid;
        }

        DataExplorer.template('a.templated:not(.site), a.templated.related-site', 'href', formatOptions);

        if (DataExplorer.Sidebar) {
            DataExplorer.Sidebar.updateHistory(response);
        }

        if (!textOnly) {
            response.resultSets.every(function (resultSet) {
                if (DataExplorer.Graph.isGraph(resultSet)) {
                    response.graph = new DataExplorer.Graph(resultSet, '#graph');

                    return false;
                }

                return true;
            });
        }

        $('#query-results .miniTabs a.optional').each(function () {
            $(this).toggleClass('hidden',
                !(response[this.hash.substring(1)] && response[this.hash.substring(1)].length !== 0));
        });

        var gridPanel = $('#resultSets').empty();
        var gridToggle = DataExplorer.template('#resultSetsTab .tab-counter', 'text', {
            current: 1,
            total: response.resultSets.length
        }).data('current-subpanel', 1).toggle(response.resultSets.length > 1).off('click');

        // We have to start showing the contents so that SlickGrid can figure
        // out the heights of its components correctly
        $('.result-option').fadeIn('fast').promise().done(function () {
            var tabset = $('#query-results .miniTabs a').off('show'),
                firstTab = tabset.filter(':not(.hidden):first'),
                selectedTab;

            if (window.location.hash) {
                selectedTab = $(window.location.hash + 'Tab');

                if (!selectedTab.length || selectedTab.hasClass('hidden')) {
                    selectedTab = null;
                }
            }
            
            if (response.graph) {
                tabset.on('show', function (event, panel) {
                    if (panel === '#graph' && !response.graph.isInitialized()) {
                        response.graph.show();
                    }
                });
            }

            var permalink = $('#permalink a')[0];

            if (permalink) {
                tabset.off('click.permalink');
                tabset.on('click.permalink', function () {
                    // We should probably be using the formatter here, but for now we'll
                    // just hack around it because this whole mess needs tidying anyway
                    permalink.href = permalink.href.replace(/#.*$/, '') + (this != firstTab[0] ? this.hash : '');
                });
            }

            if (response.executionPlan && QP && typeof QP.drawLines === 'function') {
                $('#executionPlan').html(response.executionPlan);
            }

            if (!textOnly) {
                var grids = [];

                for (var i = 0; i < response.resultSets.length; ++i) {
                    gridPanel.append(document.create('div', { className: 'subpanel' }));
                    grids.push(new DataExplorer.ResultSet(
                        response.resultSets[i],
                        {
                            url: response.url,
                            name: response.siteName
                        },
                        '#resultSets .subpanel:nth-child(' + (i + 1) + ')'
                    ));
                }

                function showCurrentGrid() {
                    var index = gridToggle.data('current-subpanel');

                    if (!$('#resultSets .subpanel:nth-child(' + index + ')').is(':visible')) {
                        $('#resultSets .subpanel').hide().filter(':nth-child(' + index + ')').show();

                        if (!grids[index - 1].isInitialized()) {
                            grids[index - 1].show();
                        } else {
                            grids[index - 1].refresh();
                        }
                    }
                }

                tabset.on('show', function (event, panel) {
                    if (panel === '#resultSets') {
                        showCurrentGrid();
                    }
                });
                gridToggle.on('click', function () {
                    if (gridToggle.parent().hasClass('youarehere')) {
                        var index = gridToggle.data('current-subpanel') + 1;

                        if (index > response.resultSets.length) {
                            index = 1;
                        }

                        DataExplorer.template(gridToggle.data('current-subpanel', index), 'text', {
                            current: index,
                            total: response.resultSets.length
                        });

                        showCurrentGrid();
                    }
                });
            }

            if (!selectedTab) {
                selectedTab = firstTab;
            }

            selectedTab.removeClass('youarehere').click();

            var height = 0,
                maxHeight = 500;

            $('#query-results .panel').each(function () {
                var currentHeight = $(this).height();

                if (currentHeight >= maxHeight) {
                    height = maxHeight;
                    return false;
                }

                height = Math.max(currentHeight, height);
            }).css({ 'height': Math.min(height, maxHeight) });

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

    function showError(response, className) {
        var msg;
        if (response && !response.error) {
            error.hide();

            return false;
        }
        if (response.line) {
            msg = 'Line ' + response.line + ': ' + response.error;
        } else {
            msg = response.error;
        }
        error.text(msg).show()[0].className = 'error-message' + (className || '');

        return true;
    }

    function showCaptcha(response) {
        var needsCaptcha = response && response.captcha;
        var target = document.getElementById('captcha');
        var display = 'none';

        if (needsCaptcha) {
            if (!grecaptcha) {
                return showError({ error: "Anonymous users must solve a captcha, but it doesn't seem to have loaded. Please try again later." });
            }

            showError({ error: "Anonymous users must solve a captcha. Please complete the captcha and submit again." });
            grecaptcha.reset();

            display = 'block';
        }

        if (target) {
            target.style = 'display: ' + display;
        }

        return needsCaptcha;
    }
});