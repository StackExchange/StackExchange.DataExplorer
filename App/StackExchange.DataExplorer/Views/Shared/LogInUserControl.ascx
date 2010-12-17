<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<%
    if (Request.IsAuthenticated)
    {
%>
        <a href="/users/<%=Current.User.Id%>"><%:Current.User.Login%></a>
        <span class="link-separator">|</span>
        <a href="/account/logout">logout</a>
<%
    }
    else
    {
%> 
        <a href="/account/login">login</a>
<%
    }
%>
