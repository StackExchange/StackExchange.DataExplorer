<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<StackExchange.DataExplorer.Models.Site>>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
  Stack Exchange Data Explorer
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <div id="mainbar-full">
    <ul class="site-list">
      <%
          foreach (Site site in Model)
          {%>
      <li>
        <div class="siteName">
          <a href="<%=site.Name.ToLower()%>/queries">
            <img src="<%=site.ImageUrl%>" alt="<%=site.Name%>" class="site" style="<%= site.ImageCss %>"></img></a>
          <p class="tagline">
            <%=site.Tagline%>
          </p>
        </div>
        <div class="totalQuestions">
            <%=site.TotalQuestions.PrettyShort()%>
          <span class="desc">questions </span>
        </div>
        <div class="totalAnswers">
          <%=site.TotalAnswers.PrettyShort()%>
          <span class="desc">answers </span>
        </div>
        <div class="totalComments">
            <%=site.TotalComments.PrettyShort()%>
            <span class="desc">comments </span>
        </div>
        <div class="totalTags">
            <%=site.TotalTags.PrettyShort()%>
          <span class="desc">tags </span>
        </div>
        <div class="latestPost">
          <span class="title">
            <%=site.LastPost.ToRelativeTimeSpanMicro()%>
          </span><span class="desc">most recent </span>
        </div>
        <div class="latestPost">
          <span class="title">&nbsp;</span>
          <span class="desc">
            <a href="<%=site.Url%>">visit site</a>
          </span>
        </div>
        <div class="clear">
        </div>
      </li>
      <%
          }%>
    </ul>
  </div>
</asp:Content>
