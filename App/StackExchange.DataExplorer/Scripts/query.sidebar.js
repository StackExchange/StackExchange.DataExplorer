DataExplorer.Sidebar = (function () {
    var schema, history, options, panel, panels, subpanels, toggle, sidebarPreference;

    function init(o) {
        schema = $('#schema');
        history = $('#history');
        subpanels = schema.add(history);
        options = o;

        panel = $(o.panel);
        panels = panel.add(o.toolbar);
        toggle = panels.find('#schema-toggle').on('click', toggleSidebar);

        initSchema();

        if (DataExplorer.options.User.isAuthenticated) {
            sidebarPreference = new DataExplorer.DeferredRequest({
                'url': '/users/save-preference/:id/HideSchema'.format({
                    'id': DataExplorer.options.User.id
                })
            });

            if (DataExplorer.options.User.hideSchema) {
                toggleSidebar(false, true);
            }
        }
    }

    function initSchema() {
        var orderSort = true;

        schema.TextAreaResizer(resizeSchema, {
            'offsetTop': schema.find('.heading').outerHeight(),
            'resizeSelector': 'ul'
        });
        schema.addClass('cm-s-' + options.editorTheme);
        schema.on('click', '.schema-table', function () {
            $(this).next('dl').toggle();
        });
        schema.find('.expand').on('click', function () {
            schema.find('dl').show();
        });
        schema.find('.collapse').on('click', function () {
            schema.find('dl').hide();
        });
        schema.find('.sort').on('click', function () {
            sortSchema(orderSort = !orderSort);
            $(this).prop('title', 'sort ' + (orderSort ? 'alphabetically' : 'normally'))
                .toggleClass('icon-sort-by-alphabet', orderSort)
                .toggleClass('icon-sort-by-order', !orderSort);
        });
    }

    function toggleSidebar(show, immediately) {
        if (show !== !!show) {
            show = toggle.hasClass('hidden');
        }

        if (!show) {
            subpanels.hide();
        }

        if (immediately !== true) {
            panels.animate({ 'width': show ? '70%' : '100%' }, 'fast', function () {
                if (show) {
                    subpanels.show();
                }
            });
        } else {
            panels.css('width', show ? '70%' : '100%');
        }

        toggle.text((show ? 'hide' : 'show') + ' sidebar').toggleClass('hidden', !show);

        if (sidebarPreference) {
            sidebarPreference.request({ value: !show });
        }
    }

    function resizeSchema() {
        var available = panel.outerHeight(),
            remaining = available - schema.outerHeight(),
            list = history.children('ul'),
            offset = history.outerHeight() - list.height();

        list.height(remaining - offset);
    }

    function sortSchema(ordered) {
        schema.children('ul').sortChildren(function (li) {
            var table = $(li);

            table.children('dl').sortChildren(
                function (dt) {
                    if (dt.tagName !== 'DT') {
                        return false;
                    }

                    return ordered ? parseInt(dt.getAttribute('data-order')) : dt[_textContent];
                },
                function (parent, element) {
                    var sibling = getNextElementSibling(element);

                    parent.appendChild(element);
                    parent.appendChild(sibling);
                }
            );

            return ordered ? parseInt(li.getAttribute('data-order')) : table.children('.schema-table').text();
        });

        
    }

    function updateHistory(response) {
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

            if (window.history && window.history.pushState && document.URL.indexOf("query/edit") == -1) {
                window.history.pushState(null, "", "/" + response.siteName + "/query/edit/" + response.querySetId);
            }
        }

        history.find('.relativetime').each(function () {
            this[_textContent] = Date.parseTimestamp(this.title).toRelativeTimeMini();
        });
    }

    return {
        init: init,
        toggle: toggleSidebar,
        updateHistory: updateHistory
    };
})();