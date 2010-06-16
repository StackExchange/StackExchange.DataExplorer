<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<StackExchange.DataExplorer.Models.User>" %>
<%@ Import Namespace="StackExchange.DataExplorer" %>

<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
   User <%= Model.Login %> - Stack Exchange Data Explorer
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="AdditionalStyles" runat="server">
</asp:Content>

<asp:Content ID="Content4" ContentPlaceHolderID="MainContent" runat="server">
  <% bool isCurrentUser = Model.Id == Current.User.Id; %>
  <div>
    <table class="vcard">
            <tr>
                <!--cell-->
                <td style="vertical-align:top; width:170px">
                    <table>
                        <tr>
                            <td style="padding:20px 20px 8px 20px">
                               <%= Model.Gravatar(128) %>
                            </td>
                        </tr>

                        <!--
                        <tr>
                            <td class="summaryinfo">
                                <div class="summarycount">10,133</div>
                                <div style="margin-top:5px; font-weight:bold">reputation</div>
                            </td>
                        </tr>

                        <tr style="height:30px">
                            <td class="summaryinfo" style="vertical-align:bottom">716 views</td>
                        </tr>
                        -->
                        
                    </table>
                </td>
                <!--cell-->
                <td style="vertical-align: top; width:350px">

                    <% if (isCurrentUser || Current.User.IsAdmin) { %>
                      <div style="float: right; margin-top: 19px; margin-right: 4px">
                        <a href="/users/edit/<%= Model.Id %>">edit</a> 
                      </div>
                    <% } %>
                    <h2 style="margin-top:20px">Registered User</h2>
                    <table class="user-details">
                        <tr>

                            <td style="width:120px">login</td>
                            <td style="width:230px" class="fn nickname"><b><%= Model.Login %></b></td>
                        </tr>
                        <tr>
                            <td>member for</td>
                            <td><span class="cool" title="<%= Model.CreationDate %>"><%= (DateTime.Now - Model.CreationDate).Value.TimeTaken() %></span></td>
                        </tr>

                        <tr>
                            <td>seen</td>
                            <td><span class="<%= Model.LastSeenDate.Temperature() %>"><%= Model.LastSeenDate.ToRelativeTimeSpan() %> </span></td>
                        </tr>
                        
                        <% if (isCurrentUser) { %>
                        <tr>
                            <td>openid</td>
                            <td><div class="no-overflow"><%= Model.UserOpenIds[0].OpenIdClaim %></div></td>

                        </tr>
                        <% } %>
                        
                        <% if (Model.Website != null && Model.Website.Length > 4) { %>
                        <tr>
                            <td>website</td>
                            <td>
                                <div class="no-overflow"><a href="http://<% = Model.Website %>" rel="me" class="url"><%= Model.Website%></a></div>                                
                            </td>
                        </tr>
                        <% } %>
                        
                        <tr>

                            <td>location</td>
                            <td class="label adr">
                                <%= Model.Location %>
                            </td>
                        </tr>
                        <tr>
                            <td>age</td>
                            <td>
                                <%= Model.Age %>
                            </td>
                        </tr>
                    </table>
                </td>

                <td style="width:390px">
                    <div id="user-about-me" class="note"><%=  Model.SafeAboutMe%></div>
                    
                    <% if (false) { %>
                    <div class="summaryinfo">
                        last activity: <%=Model.LastActivityDate.ToRelativeTime()%>> from this ip address
                    </div>
                     <% } %>

                </td>
               
            </tr>
        </table>

         <% Html.RenderPartial("SubHeader", ViewData["UserQueryHeaders"]); %>
        <ul class="querylist">
          <% var queries = ViewData["Queries"] as IEnumerable<StackExchange.DataExplorer.ViewModel.QueryExecutionViewData>; %>
          <% foreach (var query in queries) { %>
              <li> <a title="<%= Html.Encode(query.Description) %>" href="<%= query.Url %>"><%= Html.Encode(query.Name) %></a> </li> 
           <% } %>
        </ul>
        <% if (ViewData["EmptyMessage"] != null) { %>
        <h3><%= ViewData["EmptyMessage"] %></h3>
        <% } %>
  </div>
</asp:Content>
