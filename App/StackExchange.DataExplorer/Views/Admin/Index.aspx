<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
	Stack Exchange Data Explorer Administration - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
  <ul>
    <li id="refreshStats"><span style="display:none;">refreshing ...</span> <a href="/admin/refresh_stats">Update site statistics</a> (will update front page stats)</li>
  </ul>

  <%=AssetPackager.ScriptSrc("jquery")%>
  <script type="text/javascript">
    $(document).ready(function () {
      $("#refreshStats a").click(function () {
        $(this).hide();
        $("#refreshStats span").show();
        $.post(this.href, null, function () {
          $("#refreshStats a").show();
          $("#refreshStats span").hide();
        });
        return false;
      });
    });
  </script>

</asp:Content>
