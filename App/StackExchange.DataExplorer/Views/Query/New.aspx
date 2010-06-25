<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<StackExchange.DataExplorer.Models.Site>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%@ Import Namespace="StackExchange.DataExplorer.Helpers" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Query <%:Model.LongName%> - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content3" ContentPlaceHolderID="AdditionalStyles" runat="server">
    <%=AssetPackager.LinkCss("viewer_editor")%>
   <STYLE type="text/css">
      <%=Model.TagCss%> 
   </STYLE>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">

    <%
        string sql;
        var query = ViewData["query"] as Query;
        if (query != null)
        {
            sql = query.QueryBody;
        }
        else
        {
            sql = ParsedQuery.DefaultComment;
        }

        sql = Html.Encode(sql);

        var cached_results = ViewData["cached_results"] as CachedResult;
%>

   
    <div id="queryText">
        <div>
          <div id="queryInfo">
            <h2><%:(query != null && query.Name != null) ? query.Name : ParsedQuery.DEFAULT_NAME%></h2>
            <p><%:(query != null && query.Description != null)
                                  ? query.Description
                                  : ParsedQuery.DEFAULT_DESCRIPTION%></p>
          </div>
          <%=Html.Partial("AboutSite", Model)%>
          <div class="clear"></div>
        </div>
        <div class="query" style="width:70%;">
          <form id="runQueryForm" action="/query/<%=Model.Id%>" method="post"> 
            <p>
              <input type="hidden" name="siteId" value="<%=Model.Id%>" />
              <div id="sqlQueryWrapper" style="position:relative;">
                <textarea id="sqlQuery" name="sql" rows="18"><%=sql%></textarea>
              </div>
              <div class="clear"></div>
            </p>
            <a id="schemaLink" href="#">hide schema &gt;&gt;</a>
            <div id="queryParams" style="display:none;">
              <h3>Enter Parameters</h3>
            </div>
            <p id="resultsToText">
              <input type="checkbox" name="resultsToText" value="true"/> 
              <label>Results to Text</label>
            </p>
            <p id="toolbar">
                <input type="submit" value="Run Query" />

                

                <span id="permalinks" style="display:none">
                  <a id="permalink" href="#">permalink to this query</a> 
                  <a id="saveQuery" href="#">save query</a>
                  <a id="downloadCsv" href="#" title="download results as CSV">download results</a>
                  <%
        var sites = (IEnumerable<Site>) ViewData["Sites"];%> 
                  <%
        foreach (Site site in sites)
        {
            if (site.Id == Model.Id) continue;
%>
                      <a class="otherPermalink" href="/<%:site.Name.ToLower()%>/q//" title="View results on <%:site.LongName%>">
                      <img src="<%=site.IconUrl%>" alt="<%:site.LongName%>" /></a>     
                  <%
        }%>
                </span>
            </p>
          </form>
          <div class="clear"></div>
          <div id="saveDialog" style="position: relative; display: none;">
            <form id="saveQueryForm" action="#" method="post">
              <input type="hidden" name="savedQueryId" class="savedQueryId" value="<%=ViewData["SavedQueryId"]%>" />
              <div class="overlay">&nbsp;</div>
              <div class="dialog">
                <label>Title</label>
                <input type="text" name="title" value="" class="title"/>
                <textarea name="description" class="description" cols="80" rows="5"></textarea>
                <div class="savePanel">
                  <input type="submit" class="save" name="saveButton" value="Save"/>
                  <input type="button" class="cancel" name="cancelButton" value="Cancel"/>
                </div>
                <p class="error ui-state-error" style="display:none"></p>
              </div>
             </form>
          </div>
         
          <div style="padding: 5px; display:none" class="ui-state-error ui-corner-all" id="queryErrorBox"> 
					  <p><span style="float: left; margin-right: 0.3em;" class="ui-icon ui-icon-alert"></span> 
					  <strong>Error:</strong> <span id="queryError"> </span></p>
				  </div>
          <div class="clear"></div>

        </div>
        <ul class='treeview schema' style="display:none;">
          <%
        foreach (TableInfo table in (IEnumerable<TableInfo>) ViewData["Tables"])
        {%>
            <li>
              <span class="folder"><%:table.Name%></span>
              <ul>
                <%
            for (int i = 0; i < table.ColumnNames.Count; i++)
            {%>
                     
                  <li title="<%=table.DataTypes[i]%>"><%:table.ColumnNames[i]%></li>
                <%
            }%>
              </ul>
            </li>
          <%
        }%>
        </ul>
        
    </div>

    <div class="clear"></div>

    <p style="display:none" class="loading"><img src="http://sstatic.net/mso/img/ajax-loader.gif"/><span>Hold tight while we fetch your results</span></p>

    <div id="queryResults" style="display:none">
      <div id="resultTabs" class="subheader">
        <div class="miniTabs">
          <a href="#grid" class="youarehere">Results</a>
          <a href="#messages">Messages</a>
        </div>
       </div>
      <div id="grid"></div> 
      <div id="messages" style="display:none; ">
        <pre><code></code></pre>
      </div> 
     
      <div id="gridStats" class= "ui-widget-header">
        <span class="duration"></span>
        <span class="rows"></span>
        <div class="clear"></div>
      </div>
    </div>

   
  <%=AssetPackager.ScriptSrc("jquery")%>
  <%=AssetPackager.ScriptSrc("jquery.validate")%>
  <%=AssetPackager.ScriptSrc("editor")%>

  <script type="text/javascript">

    var siteId = <%=Model.Id%>;
    var loggedOn = <%=(!Current.User.IsAnonymous).ToString().ToLower()%>;

    <%=Html.Partial("GuessedUserId")%>

    var codemirror; 

    function getQueryInfo(text) {
      var info = {title: "", description: ""};
      var lines = text.split("\n"); 
      var gotTitle = false; 
      
      for (var i = 0; i < lines.length; i++) {
        if (lines[i].match("^--") == "--") {
          var data = lines[i].substring(2).replace(/^\s+|\s+$/g,"");
          if (gotTitle) {
            info.description += data + "\n";
          } else {
            info.title = data;
            gotTitle = true;
          }
        } else {
          break;
        }
      }
      return info;
    }


    $(document).ready(function () {
       
      $('.miniTabs').tabs();

      var textarea = $('#runQueryForm textarea');
      var treeview = $('.treeview.schema');

      var getQueryBody = function() {
        if (codemirror != null) 
        {
          return codemirror.getCode();
        } else {
          return textarea.val();
        }
      };

      $("#saveQuery").click(function() {
        
        if (!loggedOn) {
          this.href = "/account/login";
          return true;
        }

        $("#saveDialog .error").hide();
        var data = getQueryInfo(getQueryBody());
        $("#saveDialog .title").val(data.title);
        $("#saveDialog .description").val(data.description);
        $("#saveDialog").show();
        $("#saveDialog .title").focus();

        return false;
      });

       
      $("#saveQueryForm").validate({
        rules: {
          title: {
            required: true,
            minlength: 10
          }
        }
      });

      $("#saveQueryForm").submit(function() {

        if(!$(this).valid()){
          return false;
        }

        $("#saveDialog .error").hide();
        $("#saveDialog .save").attr("disabled", true);
        $.post('/saved_query/create', {
          
          queryId: current_results.queryId ,
          savedQueryId: $("#saveDialog .savedQueryId").val(),
          title: $("#saveDialog .title").val() ,
          description: $("#saveDialog .description").val(),
          siteId: siteId
        }, function(data) {
           $("#saveDialog .save").attr("disabled", false);
           if (data.success == true) {
              $("#saveDialog").hide(); 
           } else {
              $("#saveDialog .error").show().text(data.message);
           }
        });

        return false;
      });

      $("#saveDialog .cancel").click(function() {
        $("#saveDialog").hide();
        return false;
      });

      var oldTimeout = null; 

      var queryTitle = $("#queryInfo h2");
      var queryDescription = $("#queryInfo p");

      var updateTitle = function() {
        if (codemirror != null) {
          textarea.val(codemirror.getCode());
        } 
        var info = getQueryInfo(textarea.val()); 

        queryTitle.text(info.title);
        queryDescription.text(info.description);

      };

      var onTextChanged = function() {
        if (oldTimeout != null) {
          clearTimeout(oldTimeout); 
        }
        oldTimeout = setTimeout(updateTitle, 10);
      };

      if (CodeMirror.isProbablySupported()) 
      {
        codemirror = CodeMirror.fromTextArea('sqlQuery', {
          height: "250px",
          width: "100%",
          parserfile: "parsesql.js",
          stylesheet: "/Content/codemirror/sqlcolors.css",
          path: "/Scripts/codemirror/",
          textWrapping: false,
          tabMode: "default",
          onChange: onTextChanged,
          initCallback: function() {
            codemirror.focus();
          }
        });
      }

      $('#schemaLink').toggle(function() {
        $('.query').animate({width: "100%"}, 'fast');
        $('.schema.treeview').hide(); 
        $(this).text("<< show schema");
      }, function(){
        $('.query').animate({width: "70%"}, 'fast', function() { $('.schema.treeview').show(); });
        $(this).text("hide schema >>");
      });
      
      treeview.height(textarea.height() + 10);

      // its messy cause CodeMirror does not like the wrap method
      var queryWrapper = $('#sqlQueryWrapper');

      $('#sqlQueryWrapper').TextAreaResizer(function() { 
        treeview.height(queryWrapper.height() + 10);
        if (codemirror != null) {
          $(codemirror.wrapping).height(queryWrapper.height() - 10);
        }
      });
      
      if (codemirror == null) {
        textarea.focus();
        textarea.keydown(onTextChanged); 
      } 

      // code mirror is probably not ready yet
      ensureAllParamsEntered(textarea.val());
      populateParamsFromUrl();

      <%
        if (cached_results != null)
        {%>
       gotResults( <%=cached_results.Results%> ); 
      <%
        }%>

      $('.schema').show();

      $('#runQueryForm').submit(function () {

        var sql = getQueryBody();
        
        // ie hack 
        if (codemirror != null) 
        {
          textarea.val(sql);
        }

        executeQuery(sql);

        return false;
      });
    });
  </script>

</asp:Content>

