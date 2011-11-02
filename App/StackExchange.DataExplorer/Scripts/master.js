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

    return {
        'init': init,
        'ready': ready,
        'options': options,
        'template': template
    };
})(), _textContent = 'textContent' in document.createElement('span') ? 'textContent' : 'innerText';

DataExplorer.DeferredRequest = function DeferredRequest(settings) {
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

DataExplorer.Voting = (function () {
    var id, error, counter, star,
        voted = false, pending = false;

    function init(target, revision, vote, readOnly) {
        target = $(target);

        if (readOnly) {
            // We might want to do something like pop up an error about how
            // they can't vote on their own query here

            return;
        }

        id = revision;
        voted = vote;

        star = target.find('span').click(click);
        counter = target.find('.favoritecount');
        error = target.find('.error-notification');
    }

    function click(event) {
        if (!DataExplorer.options.User.isAuthenticated) {
            error.show();

            return;
        }

        if (pending) {
            return;
        }

        pending = true;

        $('/vote/' + id, { 'voteType': 'favorite' }, function (response) {
            if (typeof response === 'object' && response.success) {
                var count = parseInt(counter.text());

                voted = !voted;

                if (!voted) {
                    counter.removeClass('favoritecount-selected');
                    counter.text(count - 1);
                    star.removeClass('star-on');
                    star.addClass('star-off');
                } else {
                    counter.addClass('favoritecount-selected');
                    counter.text(count + 1);
                    star.removeClass('star-off');
                    star.addClass('star-on');
                }
            }

            pending = false;
        });
    }

    return {
        'init': init
    }
})();

DataExplorer.ready(function () {
    $(document).delegate('.error-notification', 'click', function () {
        $(this).hide();
    });
});

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

if (!Date.now) {
    Date.now = function () {
        return +new Date();
    }
}

Date.parseTimestamp = (function () {
    var implementation = function (timestamp) {
        return new Date(timestamp);
    };

    try {
        implementation('2011-01-01 12:00:00Z');
    } catch (ex) {
        implementation = function (timestamp) {
            var bits = timestamp.split(/[-: Z]/);

            // We only care about local and UTC, so assume non-local is always UTC
            if (typeof bits[7] === 'undefined') {
                return new Date(
                    parseInt(bits[0], 10),
                    parseInt(bits[1], 10),
                    parseInt(bits[2], 10),
                    parseInt(bits[3], 10),
                    parseInt(bits[4], 10),
                    parseInt(bits[5], 10)
                );
            } else {
                return new Date(Date.UTC(
                    parseInt(bits[0], 10),
                    parseInt(bits[1], 10),
                    parseInt(bits[2], 10),
                    parseInt(bits[3], 10),
                    parseInt(bits[4], 10),
                    parseInt(bits[5], 10)
                ));
            }
        }
    }

    return implementation;
})();

Date.prototype.toRelativeTimeMini = (function () {
    months = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];

    return function () {
        var delta = (Date.now() - this) / 1000,
            rendered = "",
            minute = 60, hour = 60 * minute, day = 24 * hour,
            minutes = this.getUTCMinutes();

        if (delta < 5) {
            rendered = 'just now';
        } else if (delta < minute) {
            rendered = Math.round(delta) + 's ago';
        } else if (delta < hour) {
            rendered = Math.round(delta / minute) + 'm ago';
        } else if (delta < day) {
            rendered = Math.round(delta / hour) + 'h ago';
        } else {
            delta = Math.round(delta / day);

            if (delta <= 2) {
                rendered = delta + 'd ago';
            } else if (delta <= 330) {
                rendered = months[this.getUTCMonth()] + ' ' + this.getUTCDate() + ' at ' + this.getUTCHours() + ':' + (minutes < 10 ? '0' : '') + minutes;
            }
        }

        return rendered;
    };
})();