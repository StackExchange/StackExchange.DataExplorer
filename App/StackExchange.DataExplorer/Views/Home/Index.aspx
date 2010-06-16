<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<StackExchange.DataExplorer.Models.Site>>" %>

<%@ Import Namespace="StackExchange.DataExplorer" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
  Stack Exchange Data Explorer
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <div id="mainbar-full">
    <ul class="site-list">
      <%foreach (var site in Model) { %>
      <li>
        <div class="siteName">
          <a href="<%= site.Name.ToLower() %>/queries">
            <img src="<%=site.ImageUrl%>" alt="<%= site.Name %>" class="site"></img></a>
          <p class="tagline">
            <%= site.Tagline %>
          </p>
        </div>
        <div class="totalQuestions">
            <%= site.TotalQuestions.PrettyShort()%>
          <span class="desc">questions </span>
        </div>
        <div class="totalAnswers">
          <%= site.TotalAnswers.PrettyShort()%>
          <span class="desc">answers </span>
        </div>
        <div class="totalComments">
            <%= site.TotalComments.PrettyShort()%>
            <span class="desc">comments </span>
        </div>
        <div class="totalTags">
            <%= site.TotalTags.PrettyShort() %>
          <span class="desc">tags </span>
        </div>
        <div class="latestPost">
          <span class="title">
            <%= site.LastPost.ToRelativeTimeSpanMicro()%>
          </span><span class="desc">most recent </span>
        </div>
        <div class="odata">
          <span class="title">
            <a href="<%=site.ODataEndpoint %>" title="Open Data Protocol endpoint for <%= site.Name%>" alt="Open Data Protocol endpoint for <%= site.Name%>"><img src="/Content/images/icon-odatafeed-24x24.png" width="24" height="24"/></a>
          </span>
          <span class="desc">
            <a href="<%=site.ODataEndpoint %>" title="Open Data Protocol endpoint for <%= site.Name%>" alt="Open Data Protocol endpoint for <%= site.Name%>">OData</a>
          </span>
        </div>
        <div class="clear">
        </div>
      </li>
      <% } %>
    </ul>
  </div>
</asp:Content>
