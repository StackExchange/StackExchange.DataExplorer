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

    function template(target, attribute, replacements) {
        if (typeof target === 'string') {
            target = $(target);
        }

        if (!target.length) {
            return;
        }

        target.each(function () {
            var key = 'tmpl-' + attribute,
                self = $(this),
                template = self.data(key);

            if (!template) {
                if (attribute === 'text') {
                    template = this.firstChild.nodeValue;
                } else {
                    template = this[attribute];
                }

                if (!template) {
                    return;
                }

                self.data(key, template);
            }

            if (attribute === 'text') {
                this.firstChild.nodeValue = template.format(replacements);
            } else {
                this[attribute] = template.format(replacements);
            }
        });
    }

    function DeferredRequest(settings) {
        if (!(this instanceof DeferredRequest)) {
            return new DeferredRequest(settings);
        }

        var options = $.extend({
                'delay': 950,
                'type': 'post'
            }, settings),
            pending = null,
            dispatched = false,
            deferred = null;

        $(document).bind('unload', function () {
            request(true);
        });

        this.request = function rerequest(data) {
            if (pending) {
                clearTimeout(pending);
            }

            if (!dispatched) {
                options.data = data;
                pending = setTimeout(request, options.delay);
            } else {
                deferred = function () {
                    rerequest(data);
                };
            }
        }

        function request(synchronous) {
            dispatched = true;
            synchronous = !!synchronous;

            $.ajax({
                'async': synchronous,
                'data': options.data,
                'type': options.type,
                'url': options.url,
                'success': response
            });
        }

        function response(response) {
            if (options.callback) {
                options.callback(response);
            }

            dispatched = false;

            if (deferred) {
                deferred();
            }
        }
    }

    return {
        'init': init,
        'ready': ready,
        'options': options,
        'template': template,
        'DeferredRequest': DeferredRequest
    };
})();

$.fn.tabs = function () {
    return this.delegate("a:not(.youarehere)", "click", function () {
        $(this.hash).show();
        $(this).addClass("youarehere")
            .trigger('show') 
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

String.prototype.format = function (replacements) {
    if (!replacements) {
        return this;
    }

    var result = this, replacement;

    for (replacement in replacements) {
        if (replacements.hasOwnProperty(replacement)) {
            var regex = new RegExp(":" + replacement + ';?', 'g');

            result = result.replace(regex, function (match, optional) {
                var sub = replacements[replacement];

                if (!sub && sub !== 0) {
                    sub = "";
                }

                return sub;
            });
        }
    }

    return result;
}

String.prototype.from = function (char, inclusive) {
    char = this.indexOf(char);

    if (typeof inclusive === 'undefined') {
        inclusive = true;
    }

    if (char < 1) {
        return this;
    }

    return this.substring(char + (inclusive ? 0 : 1));
}

String.prototype.asVariable = function () {
    var chunks = this.split(/\s+/),
        result = chunks[0].toLowerCase();

    if (chunks.length == 0) {
        return result;
    }

    for (var i = 1; i < chunks.length; ++i) {
        result = result
            + chunks[i].substring(0, 1).toUpperCase()
            + chunks[i].substring(1).toLowerCase();
    }

    return result;
}