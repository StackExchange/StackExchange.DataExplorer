DataExplorer.ResultSet = (function () {
    function ResultSet(resultSet, url, target) {
        var rows = resultSet.rows,
            columns = resultSet.columns,
            widths = estimateColumnSizes(columns, rows),
            grid;

        target = $(target);

        for (var i = 0; i < columns.length; ++i) {
            var name = columns[i].name.toLowerCase();

            columns[i] = {
                'cssClass': columns[i].type === 'Number' ? 'number' : 'text',
                'name': columns[i].name,
                'id': "col" + i,
                'field': "col" + i,
                'type': columns[i].type.toLowerCase(),
                'width': widths[i],
                'sortable': rows.length <= 5000
            };

            if (name === 'tags' || name === 'tagname') {
                columns[i].cssClass = 'tags';
            }
        }

        var grid, options = {
            'enableCellNavigation': false,
            'enableColumnReorder': false,
            'enableCellRangeSelection': false,
            'enableTextSelectionOnCells': true,
            'formatterFactory': new ColumnFormatter(resultSet, url),
            'rowHeight': 29            
        };

        this.show = function () {
            grid = new Slick.Grid(target, rows, columns, options);
            grid.onColumnsResized.subscribe(resizeResults);
            grid.onSort.subscribe(function (e, args) {
                var field = args.sortCol.field;

                args.grid.getData().sort(function (lhs, rhs) {
                    lhs = lhs[field] &&  typeof (lhs[field]) === 'object' ? lhs[field].id : lhs[field];
                    rhs = rhs[field] &&  typeof (rhs[field]) === 'object' ? rhs[field].id : rhs[field];

                    return (args.sortAsc ? 1 : -1) * (lhs == rhs ? 0 : lhs < rhs ? -1 : 1);
                });

                args.grid.invalidate();
            });

            resizeResults();

            $(window).resize(resizeResults);
        };

        this.isInitialized = function () {
            return !!grid;
        }

        this.refresh = resizeResults;

        function resizeResults() {
            var defaultWidth = document.getElementById('query').clientWidth - 2,
                availableWidth = document.documentElement.clientWidth - 100,
                canvas = target.find('.grid-canvas'),
                parent = target.closest('.panel'),
                canvasWidth = canvas.outerWidth(),
                width = 0;

            if (canvasWidth < defaultWidth || availableWidth < defaultWidth) {
                parent.width(width = defaultWidth);
            } else if (canvasWidth > availableWidth) {
                parent.width(width = availableWidth);
            } else {
                parent.width(width = canvasWidth);
            }

            if (width === defaultWidth) {
                parent.css('left', '0px');
            } else {
                parent.css('left', '-' + Math.round((width - defaultWidth) / 2) + 'px');
            }
        }
    }

    function ColumnFormatter(resultSet, siteInfo) {
        var base = siteInfo.url,
            autolinker = /^(https?|site|query):\/\/[-A-Z0-9+&@#\/%?=~_\[\]\(\)!:,\.;]*[-A-Z0-9+&@#\/%=~_\[\]](?:\|.+?)?$/i,
            dummy = document.createElement('a'),
            wrapper = dummy,
            _outerHTML = 'outerHTML';

        if (!dummy.outerHTML) {
            wrapper = document.createElement('span');
            _outerHTML = 'innerHTML';
            wrapper.appendChild(dummy);
        }

        var siteColumnName = null;

        if (resultSet) {
            resultSet.columns.forEach(function (column, index) {
                if (column.type === 'site') {
                    siteColumnName = "col" + index;

                    return false;
                }
            });
        }

        this.getFormatter = function (column) {
            if (column.name.toLowerCase() === 'tags' || column.name.toLowerCase() === 'tagname') {
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
                } else if (matches[1] === 'query') {
                    url = url.substring('query://'.length);
                    url = '/' + siteInfo.name + '/query/' + url;
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
            if (!value && value !== 0) {
                return defaultFormatter(row, cell, value, column, context);
            }

            return (new Date(value)).toUTC();
        }

        function tagFormatter(siteColumnName) {
            var siteColumnName = siteColumnName;

            return function (row, cell, value, column, context) {
                var isMultiTags;

                if (!value || !(value.match(/^[a-z0-9#.+-]+$/) || (isMultiTags = (value.search(/^(?:<[a-z0-9#.+-]+>)+$/) > -1)))) {
                    return defaultFormatter(row, cell, value, column, context);
                }

                var tags = isMultiTags ? value.substring(1, value.length - 1).split('><') : [value],
                    template = '<a class="post-tag :class" href=":base/tags/:url">:tag</a>',
                    value = '', tag;

                var url = base;
                if (siteColumnName != null) {
                    url = context[siteColumnName].url;
                }

                for (var i = 0; i < tags.length; ++i) {
                    tag = tags[i];

                    value = value + template.format({
                        'base': url,
                        'class': '',
                        'tag': tag,
                        'url': encodeURIComponent(tag)
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

                if (siteColumnName != null) {
                    currentUrl = context[siteColumnName].url + path;
                }

                return template.format({
                    'url': currentUrl + value.id,
                    'text': encodeColumn(value.title)
                });
            };
        }
    }

    function estimateColumnSizes(columns, rows) {
        var row,
            widths = [];
            sizerParent = document.createElement('div'),
            sizer = document.createElement('span'),
            maxWidth = 290;

        sizer.className = 'slick-cell';
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

        sizer[_textContent] = '';
        sizer.className = 'slick-header-column slick-header-column-sorted ui-state-default';
        sizerParent.className += ' slick-header';
        sizerParent.appendChild(document.create('span', { classname: 'slick-sort-indicator' }));
        sizerParent.appendChild(document.create('div', { classname: 'slick-resizable-handle' }));

        var controlWidth = sizerParent.childNodes[1].offsetWidth + sizerParent.childNodes[2].offsetWidth;

        for (var i = 0; i < columns.length; ++i) {
            var name = columns[i].name.toLowerCase();

            if (columns[i].type === 'Date') {
                widths[i] = 160;
            }

            if (name === 'post link') {
                widths[i] = maxWidth;
            } else {
                sizer[_textContent] = columns[i].name;

                if (sizer.offsetWidth + controlWidth > widths[i]) {
                    widths[i] = sizer.offsetWidth + controlWidth;
                }
            }

            widths[i] = Math.min(widths[i] || (50 + controlWidth), maxWidth);
        }

        document.body.removeChild(sizerParent);

        return widths;
    }

    function encodeColumn(value) {
        if (!value || !value.replace) {
            return value;
        }

        return value.replace(/[\n\r]/g, " ")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .substring(0, 400);
    }

    return ResultSet;
})();