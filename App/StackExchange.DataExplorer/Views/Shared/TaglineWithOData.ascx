<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<StackExchange.DataExplorer.Models.Site>" %>
    <a href="<%=Model.ODataEndpoint%>" title="Open Data Protocol endpoint for <%=Model.Name%>" alt="Open Data Protocol endpoint for <%=Model.Name%>" class="odata"><img src="/Content/images/icon-odatafeed-14x14.png" /></a>
<%=Model.Tagline%>

