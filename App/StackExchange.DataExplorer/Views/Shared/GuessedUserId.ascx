<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl" %>
<%
    if (ViewData["GuessedUserId"] != null)
    {%>
var guessedUserId = <%=ViewData["GuessedUserId"]%>;
<%
    }
    else
    {%>
var guessedUserId = '';
<%
    }%>
