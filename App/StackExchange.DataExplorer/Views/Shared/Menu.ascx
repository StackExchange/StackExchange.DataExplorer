<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<IEnumerable<StackExchange.DataExplorer.ViewModel.SubHeaderViewData>>" %>
<%@ Import Namespace="StackExchange.DataExplorer.ViewModel" %>
<div id="hmenus">

  <div class="nav">
      <ul>
          <%
              foreach (SubHeaderViewData item in Model.Where(i => !i.RightAlign))
              {%>
            <li>
              <a <%
                  if (item.Selected)
                  {%> class="youarehere" <%
                  }%> href="<%=item.Href%>" title="<%=item.Title%>"><%=item.Description%></a>  
            </li>
          <%
              }%>
      </ul>
  </div>
  <div style="float: right;" class="nav">
      <ul>
          <%
              foreach (SubHeaderViewData item in Model.Where(i => i.RightAlign))
              {%>
            <li style="margin-right: 0px;">
                <a <%
                  if (item.Selected)
                  {%> class="youarehere" <%
                  }%> href="<%=item.Href%>" title="<%=item.Title%>"><%=item.Description%></a>
            </li>
          <%
              }%>
      </ul>
  </div>
</div>
