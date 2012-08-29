DataExplorer.SiteSwitcher = (function () {
    var additional, template, input;

    function init(target, url) {
        template = url;
        input = $(target);

        input.one('focus', function () {
            $.get('/sites', prepareAutocomplete);
        });
    }

    function update(data) {
        additional = $.extend({}, data);
    }

    function prepareAutocomplete(sites) {
        input.autocomplete(sites, {
            minChars: 2,
            matchContains: 'word',
            autoFill: false,
            width: input.outerWidth(),
            formatItem: function (item) {
                return '<img src="' + item.IconUrl + '" class="site-icon" /> ' + htmlEncode(item.LongName);
            },
            formatMatch: function (item) {
                return htmlEncode(item.LongName + " " + item.Url);
            },
            formatResult: function (item) {
                return item.LongName;
            }
        }).result(function (event, site) {
            window.location = template.format($.extend(additional, { site: site.Name.toLowerCase() }));
        });
    }

    return {
        init: init,
        update: update
    }
})();