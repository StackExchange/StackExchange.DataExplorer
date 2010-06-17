<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
    Page not found - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <div class="subheader"> 
      <h2>
         Page Not Found
      </h2>
  </div>
  <div style="width:400px; float:left;">
    <p>
      Sorry we could not find the page requested. However, we did find a picture of <a href="http://en.wikipedia.org/wiki/Edgar_F._Codd">Edgar F. Codd </a>, the inventor of the relational data model.
    </p>
    <p>
      If you feel something is missing that should be here,  <a href="mailto:team@stackoverflow.com">contact us</a>.
    </p>
  </div>
  <img src="/Content/images/edgar.jpg" style="float:right;" />
  <br class="clear" />
  <p>&nbsp;</p>
</asp:Content>