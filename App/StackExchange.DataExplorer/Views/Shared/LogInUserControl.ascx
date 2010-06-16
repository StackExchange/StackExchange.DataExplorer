<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl" %>
<%
    if (Request.IsAuthenticated) {
%>
        <a href="/users/<%= StackExchange.DataExplorer.Current.User.Id %>">
          <%= Html.Encode(StackExchange.DataExplorer.Current.User.Login) %>
        </a>
        <span class="link-separator">|</span>
        <a href="/account/logout">logout</a>
<%
    }
    else {
%> 
        <a href="/account/login">login</a>
<%
    }
%>
