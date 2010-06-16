<%@ Page Language="C#" AutoEventWireup="true"  CodeFile="Default.aspx.cs" Inherits="_Default" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Test Page</title>
</head>
<body>
    <form id="form1" runat="server">
    <div>        
        <p>
        <a href="errors.aspx">View exception log</a>
        </p>
        <asp:Button ID="ErrorButton" runat="server" OnClick="ErrorButton_Click" Text="Throw an Exception" />
        <asp:Button ID="InnerErrorButton" runat="server" OnClick="InnerErrorButton_Click"
            Text="Throw an Inner Exception" />
        <asp:Button ID="IgnoreErrorButton" runat="server" OnClick="IgnoreButton_Click"
            Text="Throw an Ignored Exception" /></div>
    </form>
</body>
</html>
