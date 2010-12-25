<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<StackExchange.DataExplorer.ViewModel.QueryExecutionViewData>>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%@ Import Namespace="StackExchange.DataExplorer.ViewModel" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>

<asp:Content ID="aboutTitle" ContentPlaceHolderID="TitleContent" runat="server">
  Browse Queries - Stack Exchange Data Explorer
</asp:Content>
<asp:Content ID="secondary" ContentPlaceHolderID="SecondaryContent" runat="server">
  <div id="sidebar">
    <%
        var site = ViewData["Site"] as Site;%>

     <div class="module">
        <div class="summarycount al"><%=ViewData["TotalQueries"]%></div>
        <p>queries</p>
    </div>


    <div id="aboutSite" class="module">
      <img src="<%=site.ImageUrl%>" alt="<%:site.LongName%>" class="site"/>
      <p class="tagline">
        <%=site.Tagline%>
      </p>
    </div>

     <div class="module totalQuestions">
          <%=site.TotalQuestions.PrettyShort()%>
        <span class="desc">questions </span>
      </div>
      <div class="module totalAnswers">
        <%=site.TotalAnswers.PrettyShort()%>
        <span class="desc">answers </span>
      </div>
      <div class="module totalComments">
          <%=site.TotalComments.PrettyShort()%>
          <span class="desc">comments </span>
      </div>
      <div class="module totalTags">
          <%=site.TotalTags.PrettyShort()%>
        <span class="desc">tags </span>
      </div>
      <div class="module latestPost">
        <span class="title">
          <%=site.LastPost.ToRelativeTimeSpanMicro()%>
        </span><span class="desc">most recent </span>
      </div>

      <div class="module odata">
        <span class="desc image">
          <a href="<%=site.ODataEndpoint%>" title="Open Data Protocol endpoint for <%=site.Name%>" alt="Open Data Protocol endpoint for <%=site.Name%>"><img src="/Content/images/icon-odatafeed-32x32.png" width="32" height="32" alt="" /></a>
        </span>
        <span class="desc">
          <a href="<%=site.ODataEndpoint%>" title="Open Data Protocol endpoint for <%=site.Name%>" alt="Open Data Protocol endpoint for <%=site.Name%>">OData</a>
        </span>
      </div>

  </div>
</asp:Content>
<asp:Content ID="aboutContent" ContentPlaceHolderID="MainContent" runat="server">
  <div id="mainbar">
  
    <%
        if (Current.User.IsAdmin)
        {%>
    <p>
      <a href="#" class="showAdmin">show admin functions</a> <strong>|</strong> <a href="#"
        class="showSkipped">show skipped queries</a>
    </p>
    <%
        }%>
    <ul class="querylist">
      <%
        foreach (QueryExecutionViewData query in Model)
        {%>
      <li>
        <div class="favorites">
          <%
            if (query.FavoriteCount > 0)
            {%>
            <span title="You can favorite this query if you navigate to it" class="star-off"></span>
            <div class="favoritecount"><b><%=query.FavoriteCount%></b></div>
          <%
            }%>
        </div>
        <div class="views">
          <span class="totalViews"><%=query.Views%></span> 
          <span class="viewDesc"><%="view".Pluralize(query.Views)%></span>
        </div>
        <%
            if (Current.User.IsAdmin)
            {%>
        <div class="admin" style="display: none;">
          <span class="id" style="display: none;"><%=query.Id%></span> 
          <a class="feature <%=query.Featured ? "featured" : ""%>"
              href="#">
              <%=query.Featured ? "featured" : "feature"%></a> 
          <a class="skip <%=query.Skipped ? "skipped" : ""%>" href="#">
                <%=query.Skipped ? "skipped" : "skip"%></a>
        </div>
          <%}%>
        <div class="title">
          <h3>
            <a title="<%:query.Description%>" href="<%=query.Url%>">
              <%:query.Name%></a>
          </h3>
        </div>
        <div class="user">
          <%=query.LastRun.ToRelativeTimeSpanMini()%>
          <%
            if (query.Creator != null)
            {%>
          <a href="/users/<%=query.Creator.Id%>">
            <%:query.Creator.Login%></a>
          <%
            }%>
        </div>
        <div class="clear">
        </div>
      </li>
      <%
        }%>
    </ul>
    <%
        if (Current.User.IsAdmin)
        {%>
    <p>
      <a href="#" class="showAdmin">show admin functions</a> <strong>|</strong> <a href="#"
        class="showSkipped">show skipped queries</a>
    </p>
    <%
        }%>
    <%
        Html.RenderPartial("PageSizer", ViewData["PageSizer"]);%>
    <%
        Html.RenderPartial("PageNumbers", ViewData["PageNumbers"]);%>
  </div>
  <%=AssetPackager.ScriptSrc("jquery")%>
  <script type="text/javascript">


    $(document).ready(function () {

      var hideSkipped = function () {
        if (skippedVisible == false) {
          $('.admin .skipped').each(function () {
            $(this).parent().parent().hide();
          });
        }
      };

      var showSkipped = function () {
        $('.admin .skipped').each(function () {
          $(this).parent().parent().show();
        });
      };

      var skippedVisible = false;

      $(".showSkipped").toggle(function () {
        $(this).text("hide skipped queries");
        skippedVisible = true;
        showSkipped();
      }, function () {
        skippedVisible = false;
        $(this).text("show skipped queries");
        hideSkipped();
      });

      $(".showAdmin").toggle(function () {
        $(this).text("hide admin functions");
        $('.admin').show();
        hideSkipped();
      }, function () {
        $(this).text("show admin functions");
        $('.admin').hide();
        showSkipped();
      });

      $('.admin .skip').click(function () {

        var href = $(this);
        var id = $.trim(href.parent().find(".id").text());

        var featured = $(this).hasClass("skipped");

        if (featured) {
          $.post("/skip_query/" + id, { skip: false }, function () {
            href.text("skip");
            href.removeClass("skipped");
          });
        }
        else {
          $.post("/skip_query/" + id, { skip: true }, function () {
            href.text("skipped");
            href.addClass("skipped");
            hideSkipped();
          });
        }

        return false;

      });

      $('.admin .feature').click(function () {

        var href = $(this);
        var id = $.trim(href.parent().find(".id").text());

        var featured = $(this).hasClass("featured");

        if (featured) {
          $.post("/feature_query/" + id, { feature: false }, function () {
            href.text("feature");
            href.removeClass("featured");
          });
        }
        else {
          $.post("/feature_query/" + id, { feature: true }, function () {
            href.text("featured");
            href.addClass("featured");
          });
        }

        return false;

      });

    });
  </script>
</asp:Content>
