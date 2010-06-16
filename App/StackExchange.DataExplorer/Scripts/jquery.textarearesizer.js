/* 
	jQuery TextAreaResizer plugin
	Created on 17th January 2008 by Ryan O'Dell 
	Version 1.0.4
	
	Converted from Drupal -> textarea.js
	Found source: http://plugins.jquery.com/misc/textarea.js
	$Id: textarea.js,v 1.11.2.1 2007/04/18 02:41:19 drumm Exp $

	1.0.1 Updates to missing global 'var', added extra global variables, fixed multiple instances, improved iFrame support
	1.0.2 Updates according to textarea.focus
	1.0.3 Further updates including removing the textarea.focus and moving private variables to top
	1.0.4 Re-instated the blur/focus events, according to information supplied by dec

	
*/
(function ($) {
    /* private variable "oHover" used to determine if you're still hovering over the same element */
    var wrapperDiv, staticOffset;  // added the var declaration for 'staticOffset' thanks to issue logged by dec.
    var resizable; 
    var iLastMousePos = 0;
    var iMin = 32;
    var grip;
    var callback;
    var ovarlay; // very hacky used so it works with iframes 
    /* TextAreaResizer plugin */
    $.fn.TextAreaResizer = function (cb) {
        callback = cb;
        return this.each(function () {
            wrapperDiv = $(this).addClass('processed'), staticOffset = null;

            // 18-01-08 jQuery bind to pass data element rather than direct mousedown - Ryan O'Dell
            // When wrapping the text area, work around an IE margin bug.  See:
            // http://jaspan.com/ie-inherited-margin-bug-form-elements-and-haslayout
            //$(this).wrap('<div class="resizable-textarea"><span></span></div>')
            //  .parent().append($('<div class="grippie"></div>').bind("mousedown", { el: this }, startDrag));

            $(this).append($('<div class="grippie"></div>').bind("mousedown", { el: this }, startDrag));
            var grippie = $('div.grippie', $(this).parent())[0];
            resizable = $($(this).children(':visible')[0]);


            //grippie.style.marginRight = "-6px";
            grippie.style.marginRight = (grippie.offsetWidth - resizable[0].offsetWidth) + 'px';

        });
    };
    /* private functions */
    function startDrag(e) {
        wrapperDiv = $(e.data.el);
        wrapperDiv.blur();
        iLastMousePos = mousePosition(e).y;
        staticOffset = wrapperDiv.height() - iLastMousePos;
        resizable.css('opacity', 0.25);

        // hack so it works with iframes
        overlay = wrapperDiv.append("<div id='overlay' style='position: absolute; zindex: 99; background-color: white; opacity:0.01; filter: alpha(opacity = 1); left:0; top:0;'>&nbsp;</div>").find("#overlay");
        overlay.width(wrapperDiv.width());
        overlay.height(wrapperDiv.height());


        $(document).mousemove(performDrag).mouseup(endDrag);
        if (callback != null) callback();
        return false;
    }

    function performDrag(e) {
        var iThisMousePos = mousePosition(e).y;
        var iMousePos = staticOffset + iThisMousePos;
        if (iLastMousePos >= (iThisMousePos)) {
            iMousePos -= 5;
        }
        iLastMousePos = iThisMousePos;
        iMousePos = Math.max(iMin, iMousePos);

        //wrapperDiv.height(iMousePos + 'px');
        resizable.height(iMousePos + 'px');

        overlay.height(wrapperDiv.height());


        if (iMousePos < iMin) {
            endDrag(e);
        }
        if (callback != null) callback();
        return false;
    }

    function endDrag(e) {
        $(document).unbind('mousemove', performDrag).unbind('mouseup', endDrag);
        resizable.css('opacity', 1);
        //resizable.css('filter', 'alpha(opacity = 100)');
        wrapperDiv.focus();
        wrapperDiv = null;
        staticOffset = null;
        iLastMousePos = 0;
        if (callback != null) callback();
        overlay.remove();
    }

    function mousePosition(e) {
        return { x: e.clientX + document.documentElement.scrollLeft, y: e.clientY + document.documentElement.scrollTop };
    };
})(jQuery);

