<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.ViewModel.SubHeader>" %>
<% if (Model != null) { %>
<div class="subheader"> 
    <% if (Model.Title != null) { %>
      <h2>
          <%= Model.Title %>
      </h2>
    <% } %>
    <% if (Model.Items != null) { %>
    <div id="tabs"> 
        <% foreach (var item in Model.Items) { %>
        <a <% if(item.Selected) { %> class="youarehere" <% } %> href="<%= item.Href %>" title="<%= item.Title %>"><%= item.Description%></a>
        <% } %>
    </div> 
    <% } %>
</div> 
<% } %>