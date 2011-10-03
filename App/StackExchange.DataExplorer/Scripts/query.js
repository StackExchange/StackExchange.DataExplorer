DataExplorer.QueryEditor = (function () {
    var editor, field, activeError,
        events = {
            'description': [],
            'title': []
        },
        metadata = {
            'title': null,
            'description': null
        };

    function create(target, callback) {
        if (typeof target === 'string') {
            target = $(target);
        }

        field = target;
        editor = CodeMirror.fromTextArea(target[0], {
            'mode': 'text/x-t-sql',
            'lineNumbers': true,
            'onChange': onChange
        });

        if (callback && typeof callback === 'function') {
            callback(editor);
        }
    }

    function dispatch(event, value) {
        if (events[event]) {
            event = events[event];

            for (var i = 0; i < event.length; ++i) {
                event[i](value);
            }
        }
    }

    function getValue() {
        var value = editor.getValue();

        // Strip zero-width that randomly appears when copying text from the current
        // Data Explorer query editor into this one, at least until I can figure out
        // where it's coming from.
        if (value.charCodeAt(value.length - 1) === 8203) {
            value = value.substring(0, value.length - 1);

            // Explicitly update the field when this happens
            field.val(value);
        }

        return value;
    }

    function registerHandler(event, callback) {
        if (events[event] && typeof callback === 'function') {
            events[event].push(callback);
        }
    }

    function onChange() {
        parseMetadata();

        if (activeError !== null) {
            editor.setLineClass(activeError, null);

            activeError = null;
        }
    }

    function onError(line) {
        if (!DataExplorer.options.enableAdvancedSqlErrors) {
            return;
        }

        activeError = +line + parseMetadata();

        editor.setLineClass(activeError, 'error');
    }

    function parseMetadata() {
        // Determine the offset
        var offset = -1, lines = getValue().split("\n"), i,
            comments = false, title, description;

        for (i = 0; i < lines.length; ++i) {
            if (!comments && lines[i].indexOf('--') === 0) {
                lines[i] = lines[i].substring(2).trim();

                if (i === 0) {
                    title = lines[i];
                } else {
                    if (!description) {
                        description = "";
                    } else {
                        description = description + "\n";
                    }

                    description = description + lines[i];
                }

                offset++;
            } else if (/^\s*$/.test(lines[i])) {
                comments = true;
                offset++;
            } else {
                break;
            }
        }

        if (title !== metadata.title) {
            dispatch('title', metadata.title = title || "");
        }

        if (description !== metadata.description) {
            dispatch('description', metadata.description = description || "");
        }

        return offset;
    }

    return {
        'create': create,
        'value': getValue,
        'change': registerHandler,
        'error': onError
    };
})();

DataExplorer.ready(function () {
    var schema = $('ul.schema'),
        panel = $('.query'),
        metadata = $('#queryInfo');

    DataExplorer.QueryEditor.create('#sqlQuery', function (editor) {
        var wrapper, resizer, border = 2;

        if (editor) {
            wrapper = $(editor.getScrollerElement());
        }

        resizer = $('#sqlQueryWrapper').TextAreaResizer(function () {
            var height = resizer.height();

            schema.height(height - border);

            if (wrapper) {
                wrapper.height(height - 10);
                editor.refresh();
            }
        });

        schema.height(resizer.height() - border);
        schema.addClass('cm-s-' + editor.getOption('theme') + '');
        schema.delegate('.schema-table', 'click', function () {
            var self = $(this);

            self.toggleClass('closed');
            self.next('dl').slideToggle('fast');
        });
    });

    DataExplorer.QueryEditor.change('title', function (title) {
        metadata.find('h2').text(title);
    });

    DataExplorer.QueryEditor.change('description', function (description) {
        metadata.find('p').text(description);
    });

    $('.miniTabs').tabs();
    $('#schemaToggle').toggle(function () {
        schema.hide();
        panel.animate({ 'width': '100%' }, 'fast');
        $(this).text("<< show schema");
    }, function () {
        panel.animate({ 'width': '70%' }, 'fast', function () {
            schema.show();
        });
        $(this).text("hide schema >>");
    });
    $('#runQueryForm').submit(function () {
        executeQuery(DataExplorer.QueryEditor.value());

        return false;
    });
});



function encodeColumn(s) {
    if (s != null && s.replace != null) {
        s = s.replace(/[\n\r]/g, " ")
              .replace(/&(?!\w+([;\s]|$))/g, "&amp;")
              .replace(/</g, "&lt;")
              .replace(/>/g, "&gt;")
              .substring(0, 400);
        return s;
    } else {
        return s;
    }
}

function splitTags(s) {
    if (s == null) return [];
    var tmp = s.split("<");
    var rval = [];
    for (var i = 0; i < tmp.length; i++) {
        if (tmp[i] != "") {
            rval.push(tmp[i].replace(">", ""));
        }
    }

    return rval;
}

var current_results;

function scrollToResults() {
    var target_offset = $("#toolbar").offset();
    var target_top = target_offset.top - 10;
    $('html, body').animate({ scrollTop: target_top }, 500);
}

// this is from SO 901115
function getParameterByName(name) {
    name = name.replace(/[\[]/, "\\\[").replace(/[\]]/, "\\\]");
    var regexS = "[\\?&]" + name + "=([^&#]*)";
    var regex = new RegExp(regexS);
    var results = regex.exec(window.location.href);
    if (results == null)
        return "";
    else
        return decodeURIComponent(results[1].replace(/\+/g, " "));
}

function populateParamsFromUrl() {
    $('#queryParams').find("p input").each(function () {
        var val = getParameterByName(this.name);
        if (val != null && val.length > 0) {
            $(this).val(getParameterByName(this.name));
        }
    });
}

function displayCaptcha() {
    $('form input[type=submit]').hide();
    $('#captcha').show();

    $("#recaptcha_response_field").keydown(function (key) {
        if (key.keyCode == 13) {
            $("#btn-captcha").click();
            return false;
        }
        return true;
    }).focus();

    var captcha = function () {
        $(this).unbind("click");
        $.ajax({
            url: '/captcha',
            data: $('#captcha').closest('form').serialize(),
            type: 'POST',
            success: function (data) {
                if (data.success) {
                    $('form input[type=submit]').show();
                    $('#captcha').hide();
                    $('#captcha').closest('form').submit();
                } else {
                    $("#captcha-error").fadeIn();
                    $("#btn-captcha").click(captcha);
                    var text = $("#recaptcha_response_field");
                    var once = function () { $("#captcha-error").fadeOut(); text.unbind("keydown", once) };
                    text.keydown(once);
                }
            },
            dataType: "json"
        });

    };

    $("#btn-captcha").click(captcha);
}

function gotResults(results) {

    $(".loading").hide();
    $('form input[type=submit]').attr('disabled', null);

    if (results && results.captcha) {
        displayCaptcha();
        return;
    }

    current_results = results;

    if (results.error != null) {
        $("#query-errors").show();
        $("#queryError").html(results.error);

        DataExplorer.QueryEditor.error(results.line);

        return;
    }

    if (results.truncated == true) {
        $("#query-errors").show();
        $("#queryError").html("Your query was truncated, only " + results.maxResults + " results are allowed");
    }

    $("#messages pre code").text(results.messages);

    var currentParams = "?";
    currentParams += $('#queryParams').find("p input").serialize();

    if (currentParams == "?") {
        currentParams = "";
    }

    $("#permalinks").show();
    var currentLink = $("#permalink")[0];

    var linkId = results.queryId;
    var slug = results.slug == null ? "/" : "/" + results.slug;
    var linkPath = results.textOnly ? "qt" : "q";
    if (window.queryId !== undefined) {
        linkId = queryId;
        slug = "/" + querySlug;
        linkPath = results.textOnly ? "st" : "s";
    }

    currentLink.href = "/" + results.siteName + "/"+ linkPath + "/" + linkId + slug + currentParams;

    $("#downloadCsv")[0].href = "/" + results.siteName + "/" + (results.excludeMetas ? "n" : "") + (results.multiSite ? "m" : "") + "csv/" + results.queryId + currentParams;

    $(".otherPermalink").each(function () {
        // slug
        this.href = this.href.substring(0, this.href.lastIndexOf("/"));
        // query id
        this.href = this.href.substring(0, this.href.lastIndexOf("/"));
        // type
        this.href = this.href.substring(0, this.href.lastIndexOf("/") + 1) + linkPath + "/" + linkId + slug;
    });

    $("#gridStats .duration").text("Duration: " + results.executionTime + "ms");

    if (results.executionPlan && results.executionPlan.length > 0) {
        $("#planTabButton").show();

        $("#executionPlan").html(results.executionPlan);
        QP.drawLines();
        
        $("#downloadPlan")[0].href = "/" + results.siteName + "/" + (results.excludeMetas ? "n" : "") + (results.multiSite ? "m" : "") + "plan/" + results.queryId + currentParams;
        $("#downloadPlan").show();
    }
    else {
        $("#planTabButton").hide();
        $("#downloadPlan").hide();
    }

    if (results.textOnly || results.resultSets.length == 0) {
        $("#messagesTabButton").click();
        $("#resultsTabButton").hide();

        $("#queryResults").show();
        return;
    }

    $("#resultsTabButton").show();
    $("#resultsTabButton").click();
    $("#grid").show();
    $("#messages").hide();

    var model = [];
    var maxWidths = [];

    for (var c = 0; c < results.resultSets[0].columns.length; c++) {
        model.push({
            width: 60, 
            cssClass: (results.resultSets[0].columns[c].type == "Number" ? "number" : "text"),
            id: results.resultSets[0].columns[c].name,
            name: results.resultSets[0].columns[c].name,
            field: c
        });
        maxWidths.push(results.resultSets[0].columns[c].name.length);
    }

    var rows = [];
    var hasTags = false;

    for (var i = 0; i < results.resultSets[0].rows.length; i++) {
        var row = {};

        for (var c = 0; c < results.resultSets[0].columns.length; c++) {
            var data = null;
            var col = results.resultSets[0].rows[i][c];
            if (col != null && col.title != null && col.id != null) {
                var specialType = results.resultSets[0].columns[c].type; 
                var baseUrl; 
                switch (specialType) {
                    case "User": 
                        baseUrl = "/users/";
                        break;
                    case "Post":
                        baseUrl = "/questions/"
                        break;
                    case "SuggestedEdit": 
                        baseUrl = "/suggested-edits/"
                        break;
                    default:
                        baseUrl = "invalid";
                }

                data = ("<a href=\"" + results.url + baseUrl +
                col.id + "\">" + encodeColumn(col.title) + "</a>");
                if (col.title.length > maxWidths[c]) maxWidths[c] = col.title.length;
            } else if (model[c].field == "Tags" || model[c].field == "TagName") {
                // smart rendering of tags 
                var tags = splitTags(col);
                var tmpLength = tags.join(" ").length;
                if (col != null && tmpLength > maxWidths[c]) maxWidths[c] = tmpLength;
                for (var tagIndex = 0; tagIndex < tags.length; tagIndex++) {
                    tags[tagIndex] = "<a class=\"post-tag\" href=\"" + results.url + "/tags/" + tags[tagIndex] + "\">" + tags[tagIndex] + "</a>";
                };
                data = tags.join(" ");
                hasTags = true;
            } else {
                data = (encodeColumn(col));
                if (col != null && col.toString().length > maxWidths[c]) maxWidths[c] = col.toString().length;
            }
            row[c] = data;
        }
        rows.push(row);
    }

    for (var i = 0; i < model.length; i++) {
        model[i].width = maxWidths[i] * 12 > 250 ? 300 : 8 + maxWidths[i] * 12;
    }

    $("#queryResults").show();
    $("#gridStats .rows").text("" + rows.length + " row" + (rows.length == 1 ? "" : "s"));


    var options = {
        enableCellNavigation: false,
        enableColumnReorder: false,
        rowHeight: hasTags ? 35 : 25
    };

    var grid = new Slick.Grid($("#grid"), rows, model, options);
    grid.onColumnsResized = function () { $("#grid").resize() };

    scrollToResults();

    $("#queryResults").insertAfter('.page:first').css({ 'padding': '0 30px', 'margin': '0 auto' });
    $("#grid").resize();
}

function executeQuery(sql) {
    $("#permalinks, #queryResults, #query-errors").hide();

    if (!ensureAllParamsEntered(sql)) {
        return false;
    }

    $(".loading").show();
    $('#runQueryForm input[type=submit]', this).attr('disabled', 'disabled');

    $.ajax({
      cache: false,
      url: $('#runQueryForm')[0].action,
      type: "POST",
      data: $("#runQueryForm").serialize(),
      error: function () {
        alert("Something is wrong with the server!");
      },
      success: gotResults
    });

    return false;
}

function ensureAllParamsEntered(query) {
    var pattern = /##([a-zA-Z0-9]+):?([a-zA-Z]+)?##/g;
    var params = query.match(pattern);
    if (params == null) params = [];

    var div = $('#queryParams');

    var allParamsHaveValues = true;

    for (var i = 0; i < params.length; i++) {
        params[i] = params[i].substring(2, params[i].length - 2);
        var colonPos = params[i].indexOf(":");
        if (colonPos > 0) {
            params[i] = params[i].substring(0, colonPos); 
        }

        var currentParam = div.find("input[name=" + params[i] + "]");
        if (currentParam.length == 0) {
            div.append("<p><label>" + params[i] + "</label>\n" +
            "<input type=\"text\" name=\"" + params[i] + "\" value=\"\" /><div class='clear'></div></p>");
            allParamsHaveValues = false;
        } else {
            if (currentParam.val().length == 0) {
                allParamsHaveValues = false;
            }
        }
    }

    // remove extra params
    div.children("p").each(function () {
        var name = $(this).find('input').attr('name');
        var found = false;
        for (var i = 0; i < params.length; i++) {
            found = params[i] == name;
            if (found) break;
        }
        if (!found) {
            $(this).remove();
        } else {
            // auto param 
            if ($(this).find('input').val().length == 0 && name == "UserId") {
                $(this).find('input').val(guessedUserId);
            }
        }
    });

    div.toggle(params.length > 0);

    if (params.length > 0 && !allParamsHaveValues) {
        div.find('input:first').focus();
    }

    return allParamsHaveValues;
}

$(function () {
  $("#grid").resize(function () {
    var width = 0;
    $(".slick-header-column").each(function () { width += $(this).outerWidth(); });
    $.data(this, "width", width)
  }).add(window).resize(function () {
    var width = $("#grid").data("width"), docWidth = document.documentElement.clientWidth - 80;
    if(width > docWidth) width = docWidth - 80;
    if(width < 950) width = 950;
    $("#queryResults").width(width + 20);
    $("#gridStats").width(width + 10);
  });
});