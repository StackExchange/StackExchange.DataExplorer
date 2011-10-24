$.fn.tabs = function () {
    $(this).delegate("a:not(.youarehere)", "click", function () {
        if (this.id == "graphTabButton") {
            $("#gridStats").hide();
        } else {
            $("#gridStats").show();
        }
        $(this.hash).show();
        $(this).addClass("youarehere")
               .siblings(".youarehere")
               .removeClass("youarehere").each(function () {
                   $(this.hash).hide();
               });
    }).delegate("a", "click", function () {
        return false;
    });
};

var showingGraph = false;

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

function isGraph(resultSet) {

    var graph = true;

    for (var i = 0; i < resultSet.columns.length; i++) {
        var type = resultSet.columns[i]["type"];
        if (i != 0 && type == 'Date') {
            graph = false;
            break;
        }
        if (type != 'Number' && type != 'Date') {
            graph = false;
            break;
        } 
    }
    return graph;
}

function showTooltip(x, y, contents) {
    $('<div id="tooltip">' + contents + '<\/div>').css({
        position: 'absolute',
        display: 'none',
        top: y + 5,
        left: x + 5,
        border: '1px solid #fdd',
        padding: '2px',
        'background-color': '#fee',
        opacity: 0.80
    }).appendTo("body").fadeIn(200);
}

function addCommas(nStr) {
    nStr += '';
    x = nStr.split('.');
    x1 = x[0];
    x2 = x.length > 1 ? '.' + x[1] : '';
    var rgx = /(\d+)(\d{3})/;
    while (rgx.test(x1)) {
        x1 = x1.replace(rgx, '$1' + ',' + '$2');
    }
    return x1 + x2;
}

function bindToolTip(graph, suffix) {
    var previousPoint = null;
    var lastCall = 0;
    graph.bind("plothover", function (event, pos, item) {

        var toolTip;

        if (item) {

            if (previousPoint == null || previousPoint[0] != item.datapoint[0] || previousPoint[1] != item.datapoint[1]) {
                previousPoint = item.datapoint;

                $("#tooltip").remove();
                var x = item.datapoint[0].toFixed(2),
                    y = item.datapoint[1].toFixed(2);

                showTooltip(item.pageX - 10, item.pageY - 40, addCommas(parseInt(y)) + suffix);
            }
        }
        else {
            if (previousPoint != null) {
                $("#tooltip").remove();
            }
            previousPoint = null;
        }

    });
}

function renderGraph(resultSet) {

    var options = {
        legend: { position: "nw" },
        grid: { hoverable: true },
        selection: { mode: "x" },
        series: { lines: { show: true }, points: { show: true} }
    };
    if (resultSet.columns[0]["type"] == 'Date') {
        options.xaxis = { mode: "time" };
    }
    var graph = $("#graph");

    var series = [];

    for (var col = 1; col < resultSet.columns.length; col++) {
        series.push([]);
    }

    for (var row = 0; row < resultSet.rows.length; row++) {
        for (var col = 1; col < resultSet.columns.length; col++) {
            series[col - 1].push([resultSet.rows[row][0], resultSet.rows[row][col]]);
        }
    }

    $.plot(graph, series, options);
    bindToolTip(graph, "");
}

function gotResults(results) {

    $(".loading").hide();
    $('form input[type=submit]').attr('disabled', null);

    if (results && results.captcha) {
        displayCaptcha();
        return;
    }

    current_results = results;

    if (results.resultSets && isGraph(results.resultSets[0])) {
        $('#graphTabButton').show();
        var width = document.documentElement.clientWidth;
        if (width < 950) { width = 950; }
        $("#graph").width(width - 90);
        $("#queryResults").width(width - 70);
        $("#gridStats").width(width - 80);
        showingGraph = true;
        renderGraph(results.resultSets[0]);
    }
    else {
        showingGraph = false;
        $('#graphTabButton').hide();
    }

    if (results.error != null) {
        $("#queryErrorBox").show();
        $("#queryError").html(results.error);
        return;
    }

    if (results.truncated == true) {
        $("#queryErrorBox").show();
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
                if (results.resultSets[0].columns[c]["type"] == "Date") {
                    data = (new Date(col)).toString("yyyy-MM-dd HH:mm:ss");
                } else {
                    data = (encodeColumn(col));
                }
                if (data != null && data.toString().length > maxWidths[c]) maxWidths[c] = data.toString().length;
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
    $("#permalinks, #queryResults, #queryErrorBox").hide();

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

function forwardEvent(event, element) {
    if (event == "focus") {
        $(".CodeMirror-wrapping").addClass("focus");
    }
    if (event == "blur") {
        $(".CodeMirror-wrapping").removeClass("focus");
    }
}

$(function () {
    $("#grid").resize(function () {
        var width = 0;
        $(".slick-header-column").each(function () { width += $(this).outerWidth(); });
        $.data(this, "width", width)
    }).add(window).resize(function () {
        var width = $("#grid").data("width"), docWidth = document.documentElement.clientWidth - 80;
        if (width > docWidth) width = docWidth - 80;
        if (width < 950) width = 950;
        if (!showingGraph) {
               $("#queryResults").width(width + 20);
               $("#gridStats").width(width + 10);
        }
    });
});