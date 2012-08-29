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
        var schema = document.getElementById('schema');
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