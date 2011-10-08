var DataExplorer = (function () {
    var deferred = new $.Deferred(),
        options = {
            'User': {},
            'Site': {}
        };

    function init(settings) {
        if (typeof settings === 'object') {
            for (var setting in settings) {
                if (settings.hasOwnProperty(setting)) {
                    if (typeof settings[setting] === 'object') {
                        if (typeof options[setting] === 'object') {
                            $.extend(options[setting], settings[setting]);
                        }
                    } else {
                        if (setting.indexOf('.') !== -1) {
                            var option = setting.split('.');

                            if (typeof options[option[0]] === 'object') {
                                options[option[0]][option[1]] = settings[setting];
                            }
                        } else {
                            options[setting] = settings[setting];
                        }
                    }
                }
            }
        }

        $(document).ready(function () {
            deferred.resolve();
        });
    }

    function ready(callback) {
        deferred.done(callback);
    }

    return {
        'init': init,
        'ready': ready,
        'options': options
    };
})();

$.fn.tabs = function () {
    $(this).delegate("a:not(.youarehere)", "click", function () {
        $(this.hash).show();
        $(this).addClass("youarehere")
            .siblings()
            .removeClass("youarehere")
            .each(function () {
                $(this.hash).hide();
            });
    }).delegate("a", "click", function () {
        return false;
    });
};

if (!String.prototype.trim) {
    String.prototype.trim = function () {
        return this.replace(/^\s+|\s+$/g, '');
    };
}