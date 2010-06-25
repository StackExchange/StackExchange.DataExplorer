<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<StackExchange.DataExplorer.Helpers.PagedList<StackExchange.DataExplorer.Models.User>>" %>
<%@ Import Namespace="StackExchange.DataExplorer.Models" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
  Users - Stack Exchange Data Explorer
</asp:Content>
<asp:Content ID="Content3" ContentPlaceHolderID="MainContent" runat="server">
  <table id="userList">
    <tbody>
        <%
            foreach (var row in Model.ToRows(7))
            {%>
          <tr>
            <%
                foreach (User user in row)
                {%> 
                 <td><%
                    Html.RenderPartial("User", user);%></td>
            <%
                }%>
          </tr>
        <%
            }%>
    </tbody>
  </table>

  <%
            Html.RenderPartial("PageNumbers", ViewData["PageNumbers"]);%>
 
</asp:Content>
