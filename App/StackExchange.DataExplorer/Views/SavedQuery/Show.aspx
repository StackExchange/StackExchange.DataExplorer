<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<StackExchange.DataExplorer.Models.SavedQuery>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
  <%=Html.Encode(Model.Title)%> - Stack Exchange Data Explorer
</asp:Content>
<asp:Content ID="Content3" ContentPlaceHolderID="AdditionalStyles" runat="server">
  <%=AssetPackager.LinkCss("viewer_editor")%>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <div id="savedQueryTitle">
    <%=Html.Partial("QueryVoting", ViewData["QueryVoting"])%>
    <% var cached_results = ViewData["cached_results"] as CachedResult; %>
    <div class="error-notification supernovabg">
      <h2>
        Please <a href="/account/login">login or register</a> to vote for this query.</h2>
      (click on this box to dismiss)
    </div>
    <div id="query-description"><%=Html.Encode(Model.Description).Replace("\n", "<br>")%></div>
    <%=Html.Partial("AboutSite")%>
    <div class="clear">
  </div>
  </div>
  <pre id="queryBodyText"><code><%:Model.Query.BodyWithoutComments%></code></pre>
  <div class="query">
    <form id="runQueryForm" action="/query/<%=Current.Controller.Site.Id%>" method="post">
    <div>
      <input type="hidden" name="siteId" value="<%=Current.Controller.Site.Id%>" />
      <div id="sqlQueryWrapper" style="position: relative;">
        <input type="hidden" id="sqlQuery" name="savedQueryId" value="<%=Model.Id%>" />
      </div>
      <div class="clear">
      </div>
    </div>
    <div id="queryParams" style="display: none;">
      <h3>
        Enter Parameters</h3>
    </div>
    <p id="resultsToText">
      <input type="checkbox" name="resultsToText" value="true" />
      <label>
        Results to Text</label>
        <% if (AppSettings.AllowRunOnAllDbsOption) { %>
            &nbsp;&nbsp;&nbsp;
            <input type="checkbox" name="allDbs" value="true"/> 
            <label>Run on all DBs</label>
        <% } %>
              
        <% if (AppSettings.AllowExcludeMetaOption) { %>
            &nbsp;&nbsp;&nbsp;
            <input type="checkbox" name="excludeMetas" value="true"/> 
            <label>Exclude Metas</label>
        <% } %>
    </p>

    <% if (Current.User.IsAnonymous) { %>
    <%= Html.Partial("Captcha", Current.NewRecaptchControl()) %>
    <% } %>

    <p id="toolbar">
      <input type="submit" value="Run Query" />
      <span class='actions'>
        <a id="hideSql" href="#">hide sql</a> 
        <a id="showSql" href="#" style="display: none;">show sql</a> 
        <%
        string editHref = string.Format("/{0}/qe/{1}/{2}",
                                        Current.Controller.Site.Name.ToLower(),
                                        Model.Id, Model.Title.URLFriendly());
%>
        <a id="editQuery" href="<%=editHref%>"><%=(Model.UserId == Current.User.Id || Current.User.IsAdmin) ? "edit" : "clone"%></a>
        <%
        if (Model.UserId == Current.User.Id || Current.User.IsAdmin)
        {%>
           <a id="deleteQuery" href="#"><%=Model.IsDeleted ?? false ? "undelete" : "delete"%></a>
        <%
        }%>
      </span>
      <span id="permalinks" style="display: none">
        <a id="permalink" href="#">permalink to this query</a> 
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
    <div class="clear">
    </div>
    <div style="padding: 5px; display: none" class="ui-state-error ui-corner-all"
      id="queryErrorBox">
      <p>
        <span style="float: left; margin-right: 0.3em;" class="ui-icon ui-icon-alert"></span>
        <strong>Error:</strong> <span id="queryError"></span>
      </p>
    </div>
    <div class="clear">
    </div>
  </div>
  <p style="display: none" class="loading">
    <img src="http://sstatic.net/img/progress-dots.gif" alt="running..." /><span>Hold tight while we fetch your results</span></p>
  <div id="queryResults" style="display: none">
    <div id="resultTabs" class="subheader">
      <div class="miniTabs">
        <a href="#grid" class="youarehere">Results</a> <a href="#messages">Messages</a>
      </div>
    </div>
    <div id="grid">
    </div>
    <div id="messages" style="display: none;">
      <pre><code></code></pre>
    </div>
    <div id="gridStats" class="ui-widget-header">
      <span class="duration"></span><span class="rows"></span>
      <div class="clear">
      </div>
    </div>
  </div>
  <%=AssetPackager.ScriptSrc("jquery")%>
  <%=AssetPackager.ScriptSrc("viewer")%>
  
  <script type="text/javascript">
    var inProgress = false;
    var queryId = <%=Model.Id%>;
    var loggedOn = <%=ViewData["LoggedOn"]%>;
    var queryText;
    var querySlug = "<%=Model.Title.URLFriendly()%>";

    <%=Html.Partial("GuessedUserId")%>

    $(document).ready(function () {

      $('.miniTabs').tabs();

      var codeBlock = $('pre#queryBodyText code');
      queryText = codeBlock.text();
      ensureAllParamsEntered(queryText);

      populateParamsFromUrl();


      <%
        if (cached_results != null)
        {%>
       gotResults( <%=cached_results.Results%> ); 
      <%
        }%>

      codeBlock.text("");
      highlightText(queryText, codeBlock[0]);

      $("#hideSql").click(function(){
        $("#queryBodyText").slideUp('slow',  function() { $("#showSql").scroll(); }); 
        $(this).hide();
        $('#showSql').show();
        return false;
      });

      
      $("#showSql").click(function(){
        $("#queryBodyText").slideDown('slow', function() { $("#hideSql").scroll(); }); 
        $(this).hide();
        $('#hideSql').show();
        return false;
      });

      $(".error-notification").click(function() {
        $(this).hide();
      });

      var deleteInProgress = false;
      $("#deleteQuery").click(function () {
        if (deleteInProgress) { return false; }
        var href = $(this); 
        var deleting = href.text() != "undelete";
        var url = deleting ? "/saved_query/delete" : "/saved_query/undelete"; 

        href.text(deleting ? "deleting ..." : "undeleting ...");
       
        $.post(url, {id: queryId}, function(result){
          deleteInProgress = false;
          if (result == "success") {
            href.text(deleting ? "undelete" : "delete");
          } else {
            alert(result);
            href.text(!deleting ? "undelete" : "delete");
          }
        }, "json");
        return false;
      });

      $("#query-voting span").click(function () {
        if (inProgress) { return; }

        if (!loggedOn) {
          $('.error-notification').show();
          return;
        }

        inProgress = true;
        var voted = $("#query-voting .favoritecount-selected").length > 0;

        var counter = $("#query-voting .favoritecount b");
        var image = $("#query-voting span");

        var voteCount = parseInt(counter.text());

        if(voted) {
          counter.removeClass("favoritecount-selected");
          counter.text(voteCount - 1);
          image.removeClass("star-on");
          image.addClass("star-off");
        } else {
          counter.addClass("favoritecount-selected");
          counter.text(voteCount + 1);
          image.removeClass("star-off");
          image.addClass("star-on");
        }

        $.post("/vote/" + queryId, { voteType: 'favorite' }, function () {
          inProgress = false;
        });

      });

       $('#runQueryForm').submit(function () {
        executeQuery(queryText);
        return false;
       });

    });
  </script>
</asp:Content>
