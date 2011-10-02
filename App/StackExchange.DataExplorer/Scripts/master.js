var DataExplorer = (function () {
    var deferred = new $.Deferred();

    function init(options) {

        $(document).ready(function () {
            deferred.resolve();
        });
    }

    function ready(callback) {
        deferred.done(callback);
    }

    return {
        'init': init,
        'ready': ready
    };
})();

$.fn.tabs = function () {
    $(this).delegate("a:not(.youarehere)", "click", function () {
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