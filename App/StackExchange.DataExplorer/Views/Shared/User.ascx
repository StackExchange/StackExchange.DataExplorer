<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.Models.User>" %>

<div class="user">
  <%=Model.Gravatar(32)%>
  <a href='/users/<%=Model.Id%>'><%=Model.Login%></a>
</div>
 
      