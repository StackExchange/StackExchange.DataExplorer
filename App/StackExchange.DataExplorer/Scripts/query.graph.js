DataExplorer.Graph = (function () {
    function Graph(resultSet, target) {
        var options = {
            legend: { position: 'nw' },
            grid: { hoverable: true },
            selection: { mode: 'x' },
            series: { lines: { show: true }, points: { show: true } }
        };

        target = $(target);

        if (resultSet.columns[0].type === 'Date') {
            options.xaxis = { mode: 'time' };
        }

        var series = [],
            graph;

        // if the second column is text we need to unpivot
        if (resultSet.columns[1].type === 'Text') {
            var columns = {};

            for (var row = 0; row < resultSet.rows.length; row++) {
                var columnLabel = resultSet.rows[row][1],
                    columnName = "col_" + columnLabel;

                if (columns[columnName] === undefined) {
                    columns[columnName] = (series.push({ label: columnLabel, data: [] }) - 1);
                }

                series[columns[columnName]].data.push([resultSet.rows[row][0], resultSet.rows[row][2]]);
            }
        } else {
            for (var col = 1; col < resultSet.columns.length; col++) {
                series.push({ label: resultSet.columns[col].name, data: [] });
            }

            for (var row = 0; row < resultSet.rows.length; row++) {
                for (var col = 1; col < resultSet.columns.length; col++) {
                    series[col - 1].data.push([resultSet.rows[row][0], resultSet.rows[row][col]]);
                }
            }
        }

        this.show = function () {
            graph = $.plot(target, series, options);
            bindTooltip(target);
        };

        this.isInitialized = function () {
            return !!graph;
        }
    }

    var tooltip;

    function bindTooltip(graph) {
        if (!tooltip) {
            tooltip = $(document.create('div', { className: 'graph-tooltip' }, document.body));
        }

        graph.on('plothover', function (event, position, item) {
            if (item) {
                tooltip.text(item.datapoint[1].prettify()).css({
                    top: item.pageY - 35,
                    left: item.pageX - 5
                }).show();
            } else {
                tooltip.hide();
            }
        });
    }

    Graph.isGraph = function (resultSet) {
        if (!resultSet || resultSet.columns.length < 2) {
            return false;
        }

        var isGraph = true;

        for (var i = 0; i < resultSet.columns.length && isGraph; i++) {
            var type = resultSet.columns[i].type;

            // allow for strings in the second column provided there are only 3 cols 
            if (type === 'Text' && i === 1 && resultSet.columns.length == 3) {
                continue;
            }

            isGraph = type === 'Number' || (type === 'Date' && i === 0);
        }

        return isGraph;
    };

    return Graph;
})();