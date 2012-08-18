DataExplorer.SiteSwitcher = (function () {
    var selected, additional, template, button, input, background;

    function init(target, url) {
        template = url;
        input = $(target);
        button = input.next('button');
        background = input.css('background-image');

        input.one('focus', function () {
            $.get('/sites', prepareAutocomplete);
        });
        button.on('click', redirect);
    }

    function update(data) {
        additional = $.extend({}, data);
    }

    function redirect() {
        window.location = template.format($.extend(additional, { site: selected.Name.toLowerCase() }));
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
            selected = site;
            button.prop('disabled', selected == null);

            if (selected != null) {
                input.css({ 'background-image': 'url(' + site.IconUrl + ')' });
            }
        });
    }

    return {
        init: init,
        update: update
    }
})();