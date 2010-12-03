<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<IEnumerable<StackExchange.DataExplorer.Models.OpenIdWhiteList>>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Stack Exchange Data Explorer Administration - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <table> 
    <thead>
        <tr>
            <th>Url</th>
            <th>Approved?</th>
        </tr>
    </thead>
    <% foreach (var entry in Model) {  %>
        
        <tr>
            <td><%: entry.OpenId %></td>
            <td class="approved"><%: entry.Approved %></td>
            <td><%: entry.CreationDate %></td>
            <td><%= entry.Approved ? "" : "<a href='/admin/whitelist/approve/" + entry.Id + "' class='approve'>approve</a>"%> </td>
            <td><%= "<a href='/admin/whitelist/remove/" + entry.Id + "' class='remove'>remove</a>"%> </td>
        </tr>

    <%} %>
  </table>

  <%=AssetPackager.ScriptSrc("jquery")%>
  <script type="text/javascript">
      $(document).ready(function () {
          $("a.approve").click(function () {
              var href = $(this);
              href.hide();
              $.post(this.href, null, function () {
                  href.parent().parent().find('.approved').text('True');
              });
              return false;
          });

          $("a.remove").click(function () {
              var href = $(this);
              href.hide();
              $.post(this.href, null, function () {
                  href.parent().parent().hide();
              });
              return false;
          });
      });
  </script>

</asp:Content>
