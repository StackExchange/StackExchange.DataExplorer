/// <reference path="third-party/jquery-1.3.2-vsdoc2.js"/>

$(function() {

    var loader = '<img class="ajax-loader" src="http://sstatic.net/img/progress-dots.gif" title="loading..." alt="loading..." width="11" height="11" />';
    var removeLoader = function() { $('.ajax-loader').fadeOut('fast'); };
    
    // update title on main error log page to show count
    $('#errorcount').each(function() { // when count > 1, #errorcount is a div; when 0, it's a span
        var count = $('table#ErrorLog td.type-col').length;
        document.title = count + ' Error' + (Math.abs(count) == 1 ? '' : 's');
    });
    
    // ajax the error deletion
    $('table#ErrorLog a.delete-link').click(function() {
        var jThis = $(this);
        
        // if we've "protected" this error, confirm the deletion
        if (jThis.parent().find('a.protect-link').length == 0 && !confirm('Really delete this protected error?')) return false;        
        
        var jRow = jThis.closest('tr');
        var href = jThis.attr('href');
        jThis.attr('href', 'javascript:void(0)');
        
        jThis.parent().prepend(loader);
        jThis.remove();
        
        $.ajax({
            type: 'GET',
            url: href,
            dataType: 'html',
            success: function(result) {
                jRow.remove();
                removeLoader();
            },
            error: function(res, textStatus, errorThrown) {
                removeLoader();
                alert('Error occurred when trying to delete');
            }
        });
    });    
    
    // ajax the protection
    $('table#ErrorLog a.protect-link').click(function() {
        var jThis = $(this);
        var href = jThis.attr('href');
        jThis.attr('href', 'javascript:void(0)');
        
        jThis.parent().append(loader);
        jThis.remove();
        
        $.ajax({
            type: 'GET',
            url: href,
            dataType: 'html',
            success: function(result) {
                removeLoader();
            },
            error: function(res, textStatus, errorThrown) {
                removeLoader();
                alert('Error occurred when trying to delete');
            }
        });
    });
    
    // allow clicks to finding users based on cookies and ips
    $('td.ip-address, div#ServerVariables td:contains("REMOTE_ADDR") + td').each(function() {
        var jThis = $(this);
        jThis.wrap('<a href="/admin/users-with-ip/' + jThis.text() + '" target="_blank"></a>');
    });
    
    $('div#Cookies td:contains("user") + td').each(function() {
        var jThis = $(this);
        var series = jThis.text().match(/s=(.*)/);
        if (series.length > 0) {
            jThis.wrap('<a href="/admin/users-with-cookie-series/' + series[1] + '" target="_blank"></a>');
        }
    });    
    
});