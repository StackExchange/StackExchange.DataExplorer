using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;

public partial class _Default : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Trace.Write("Testing", "Page_Load event fired!");
    }

    protected void IgnoreButton_Click(object sender, EventArgs e)
    {
        Trace.Write("Testing", "Ignore error button was clicked!");
        throw new Exception("This exception should be ignored because it contains the word 'albacore'! (see web.config)");
    }

    protected void ErrorButton_Click(object sender, EventArgs e)
    {
        Trace.Write("Testing", "Error button was clicked!");
        throw new Exception("This is a demo exception.");
    }

    protected void InnerErrorButton_Click(object sender, EventArgs e)
    {
        Trace.Write("Testing", "Inner error button was clicked!");
        one();
    }

    private void one()
    {
        try
        {
            two();
        }
        catch (Exception ex)
        {
            throw new InvalidCastException("SubTwo handled an exception", ex);
        }

    }

    private void two()
    {
        int x = 0;
        int y = 1;
        double z;
        z = y / x;
    }
}