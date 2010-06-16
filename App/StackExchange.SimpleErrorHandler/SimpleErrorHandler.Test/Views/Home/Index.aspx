<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="indexTitle" ContentPlaceHolderID="TitleContent" runat="server">
    Home Page
</asp:Content>

<asp:Content ID="indexContent" ContentPlaceHolderID="MainContent" runat="server">
    <p>
        <a href="/home/errors">Error Log</a><br />
        <a href="/home/errors/test">Test Exception</a><br />
        <a href="/home/errors/testinner">Test Inner Exception</a><br />
        <a href="/home/errors/testignoredsame">Test Ignored Exception</a><br />
        <a href="/home/errors/testignoreddescendent">Test Ignored Descendent Exception</a><br />
    </p>
</asp:Content>