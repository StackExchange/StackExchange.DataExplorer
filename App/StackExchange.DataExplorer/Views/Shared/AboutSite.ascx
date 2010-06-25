<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<object>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>
<%
    var site = Model as Site;
    if (site == null)
    {
        site = Current.Controller.Site;
    }%>
<div id="aboutSite">
  <img class="site" src="<%=site.ImageUrl%>" alt="<%:site.LongName%>" />
  <p class="tagline">
    <%:site.Tagline%>
  </p>
</div>

