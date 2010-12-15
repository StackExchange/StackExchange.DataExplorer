<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="aboutTitle" ContentPlaceHolderID="TitleContent" runat="server">
    About - Stack Exchange Data Explorer 
</asp:Content>

<asp:Content ID="aboutContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="content-page"> 
    <p>
        Stack Exchange Data Explorer allows you to run arbitrary queries on the Stack Exchange public data dumps.
    </p>
    <p>
     Stack Exchange Data Explorer is <b>Open Source</b>. 
     If you would like to help us make it more awesome, <a href="http://code.google.com/p/stack-exchange-data-explorer/">check out the code</a>.
    </p>
    <p>
       The Stack Exchange trilogy data dumps are hosted at <a href="http://www.clearbits.net/torrents/1117-may-10">ClearBits!</a>. 
       You can subscribe via <a href="http://www.clearbits.net/feeds/creator/146-stack-overflow-data-dump.rss">RSS</a> and be notified every time a new dump is available. 
       Have fun remixing and reusing; all we ask is for proper attribution.
    </p>
    <p>
      We also offer <a href="http://www.odata.org/">OData</a> endpoints for all the Stack Exchange Sites. 
    </p>
    </div>
</asp:Content>
