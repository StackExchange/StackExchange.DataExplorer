<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.Models.User>" %>
<%@ Import Namespace="StackExchange.DataExplorer.Helpers" %>

<div class="user">
  <%=Model.Gravatar(32)%>
  <a href='/users/<%=Model.Id%>'><%= HtmlUtilities.Encode(Model.Login) %></a>
</div>
 
      