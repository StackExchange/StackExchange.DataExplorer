<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<object>" %>
<%
  var site = Model as StackExchange.DataExplorer.Models.Site; 
  if (site == null) { site = StackExchange.DataExplorer.Current.Controller.Site; } %>
<div id="aboutSite">
  <img class="site" src="<%= site.ImageUrl %>" alt="<%: site.LongName %>" />
  <p class="tagline">
    <%: site.Tagline %>
  </p>
</div>

