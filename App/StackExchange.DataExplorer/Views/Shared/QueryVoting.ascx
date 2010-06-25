<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.ViewModel.QueryVoting>" %>
<div id="query-voting">
  <span title="This is a favorite query (click again to undo)" class="star-<%=Model.HasVoted ? "on" : "off"%>"></span>
  <div class="favoritecount"><b class="<%=Model.HasVoted ? "favoritecount-selected" : ""%>"><%=Model.TotalVotes%></b></div>
</div>

