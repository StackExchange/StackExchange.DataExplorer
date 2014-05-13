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
                    if (settings[setting] && typeof settings[setting] === 'object') {
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
            return target;
        }

        return target.each(function () {
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

    function token(object) {
        var name = '__RequestVerificationToken',
            token = $('input[name="' + name + '"]').val();

        if (!object) {
            return token;
        }

        object[name] = token;

        return object;
    }

    return {
        'init': init,
        'ready': ready,
        'options': options,
        'template': template,
        'token': token
    };
})(), _textContent = 'textContent' in document.createElement('span') ? 'textContent' : 'innerText';

DataExplorer.DeferredRequest = function DeferredRequest(settings) {
    if (!(this instanceof DeferredRequest)) {
        return new DeferredRequest(settings);
    }

    var options = $.extend({
            'delay': 950,
            'type': 'post',
            force: true
        }, settings),
        pending = null,
        dispatched = false,
        canceled = false,
        deferred = null;

    if (options.force) {
        $(document).bind('unload', function () {
            request(true);
        });
    }

    this.request = function rerequest(data) {
        if (pending) {
            clearTimeout(pending);
        }

        canceled = false;

        if (!dispatched) {
            options.data = data;
            pending = setTimeout(request, options.delay);
        } else {
            deferred = function () {
                rerequest(data);
            };
        }
    }

    this.cancel = function cancel() {
        clearTimeout(pending);
        canceled = true;
    }

    function request(synchronous) {
        dispatched = true;
        synchronous = !!synchronous;

        var data = typeof options.data === 'function' ? options.data() : options.data;

        if (!canceled) {
            $.ajax({
                'async': synchronous,
                'data': data,
                'type': options.type,
                'url': options.url,
                'success': response
            });
        } else {
            dispatched = false;
        }
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

        $.post('/vote/' + id, { 'voteType': 'favorite' }, function (response) {
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

$.fn.tabs = function (passthrough) {
    return this.delegate("a:not(.youarehere)", "click", function () {
        $(this.hash).show();
        $(this).addClass("youarehere") 
            .siblings()
            .removeClass("youarehere")
            .each(function () {
                $(this.hash).hide();
            })
            .end()
            .trigger('show', [ this.hash ]);
    }).delegate("a", "click", function () {
        return !passthrough;
    });
};

$.fn.sortChildren = function (map, insert) {
    return this.each(function () {
        var i, mapped = [],
            children = this.children,
            length = children.length,
            value;

        for (i = 0; i < length; ++i) {
            value = map(children[i]);

            if (value !== false) {
                mapped.push({
                    node: children[i],
                    value: map(children[i])
                });
            }
        }

        mapped.sort(function (lhs, rhs) {
            if (lhs.value === rhs.value) {
                return 0;
            }

            return lhs.value > rhs.value ? 1 : -1;
        });

        if (!insert) {
            insert = function (parent, node) {
                parent.appendChild(node);
            }
        }

        length = mapped.length;

        for (i = 0; i < length; ++i) {
            insert(this, mapped[i].node);
        }
    });
};

document.create = function create(element, attributes, appendTo) {
    element = document.createElement(element);

    if (attributes) {
        if (attributes.text != null) {
            element[_textContent] = attributes.text;
            delete attributes.text;
        }

        if (attributes.innerHTML != null) {
            element.innerHTML = attributes.innerHTML;
            delete attributes.innerHTML;
        }

        var i, keys = Object.keys(attributes);

        for (i = 0; i < keys.length; ++i) {
            var key = keys[i];

            if (key.toLowerCase() === 'classname') {
                key = 'class';
            }

            element.setAttribute(key, attributes[keys[i]]);
        }
    }

    if (appendTo) {
        appendTo.appendChild(element);
    }

    return element;
};

function getNextElementSibling(element) {
    if (element.nextElementSibling) {
        return element.nextElementSibling;
    }

    do {
        element = element.nextSibling;
    } while (element && element.nodeType !== 1);

    return element;
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
};

String.prototype.toCamelCase = function () {
    return this.charAt(0).toLowerCase() + this.substring(1);
};

String.prototype.from = function (ch, inclusive) {
    ch = this.indexOf(ch);

    if (typeof inclusive === 'undefined') {
        inclusive = true;
    }

    if (ch < 1) {
        return this;
    }

    return this.substring(ch + (inclusive ? 0 : 1));
};

window.location.param = (function () {
    var cache = null;

    return function (name) {
        if (cache === null) {
            cache = {};

            if (window.location.search.length < 4) {
                cache = false;

                return;
            }

            var search = window.location.search.substring(1),
                groups = search.split('&'), i;

            for (i = 0; i < groups.length; ++i) {
                search = groups[i].split('=');

                if (search.length === 2 && search[0].length) {
                    cache[search[0].toLowerCase()] = window.decodeURIComponent(search[1].replace(/\+/g, ' '));
                }
            }
        }

        if (cache === false) {
            return;
        }

        return cache[name.toLowerCase()];
    }
})();

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

Date.prototype.toUTC = (function () {
    function zero(val) {
        return val < 10 ? '0' + val : val;
    }

    return function () {
        return this.getUTCFullYear() +
            '-' + zero(this.getUTCMonth() + 1) +
            '-' + zero(this.getUTCDate()) +
            ' ' + zero(this.getUTCHours()) +
            ':' + zero(this.getUTCMinutes()) +
            ':' + zero(this.getUTCSeconds());
    }
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

Number.prototype.prettify = function () {
    var number = this,
        current,
        formatted = '',
        negative = number < 0;

    if (negative) {
        number = -number;
    }

    if (number < 1000) {
        return (negative ? '-' : '') + (Math.floor(number) == number ? number: number.toFixed(2));
    }

    do {
        current = number / 1000;
        number = Math.floor(current);
        current = Math.round((current - number) * 1000);

        formatted = current + formatted;

        if (number > 0) {
            formatted = (current < 100 ? '0' : '') + (current < 10 ? '0' : '') + formatted;
        }
    } while (number > 0 && (formatted = ',' + formatted));

    return (negative ? '-' : '') + formatted;
};

function htmlEncode(text) {
    return document.createElement("div").appendChild(document.createTextNode(text)).parentNode.innerHTML;
}

DataExplorer.initComposeButton = function (site) {
    // Add the site selection button to the right of Compose Query
    var button = document.getElementById('compose-button'),
        list = button.parentNode.parentNode,
        item = document.create('li', {
            className: 'site-selector-arrow ' + button.className,
            innerHTML: "&#9662;"
        }),
        icon = document.create('img', {
            src: site.IconUrl,
            alt: site.LongName,
            title: "Switch site from " + site.LongName
        }),
        wrapper = document.create('span', {
            className: 'site-icon-wrapper'
        });

    wrapper.appendChild(icon);
    item.insertBefore(wrapper, item.childNodes[0]);
    list.appendChild(item);

    // Set up the hidden selection box
    var popup = document.create('div', {
            className: 'site-selector-popup'
        }),
        loader = document.create('p', {
            className: 'loading',
            text: 'Loading list of sites...'
        });

    popup.appendChild(loader);
    list.parentNode.appendChild(popup);

    popup = $(popup).click(function () { return false; });
    item = $(item).click(togglePopup);
    $(document).click(hidePopup);

    var nav = $(list.parentNode),
        input,
        displayed = false,
        fetchSites = true;

    function togglePopup() {
        if (!displayed) {
            showPopup();
        } else {
            hidePopup();
        }

        return false;
    }

    function showPopup() {
        if (fetchSites) {
            fetchSites = false;

            popup.css({
                top: (item.offset().top + item.outerHeight()),
                left: ((nav.offset().left + nav.outerWidth()) - popup.outerWidth())
            });

            if (DataExplorer.options.WhitelistEnabled && !DataExplorer.options.User.isAuthenticated) {
                loader[_textContent] = "Please log in first";
            } else {
                $.get('/sites', prepareAutocomplete);
            }
        }

        item.addClass('youarehere');
        popup.show();
        displayed = true;

        if (input) {
            input.focus();
        }
    }

    function hidePopup() {
        if (button.className !== 'youarehere') {
            item.removeClass('youarehere');
        }

        popup.hide();
        displayed = false;

        if (input) {
            input.val(null);
        }
    }

    function prepareAutocomplete(sites) {
        input = document.create('input', {
            placeholder: 'search by name or url'
        });

        var container = document.create('div', {
            className: 'ac_results'
        });

        loader.parentNode.replaceChild(input, loader);
        input.parentNode.appendChild(container);

        input = $(input);

        input.width(popup.width() - (input.outerWidth(true) - input.width()));
        input.autocomplete(sites, {
            container: $(container),
            minChars: 2,
            matchContains: 'word',
            autoFill: false,
            width: '100%',
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
            window.location = "/" + site.Name.toLowerCase() + "/query/new";
        });
        input.focus();
    }
};