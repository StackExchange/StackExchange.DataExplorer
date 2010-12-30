<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.Models.User>" %>
<%@ Import Namespace="StackExchange.DataExplorer.Helpers" %>

<div class="user">
    <div class="user-gravatar"><a href="/users/<%= Model.Id %>"><%= Model.Gravatar(32) %></a></div>
    <div class="user-details"><a href="/users/<%= Model.Id %>"><%= HtmlUtilities.Encode(Model.Login) %></a></div>
</div>
