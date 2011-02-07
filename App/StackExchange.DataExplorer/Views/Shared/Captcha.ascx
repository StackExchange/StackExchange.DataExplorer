<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<Recaptcha.RecaptchaControl>" %>           
<div id="captcha" style="display:none">
    <h4>To avoid spam queries, all anonymous users must solve a captcha</h4>
    <div><%
                StringBuilder sb = new StringBuilder(); 
                HtmlTextWriter writer = new HtmlTextWriter(new System.IO.StringWriter(sb));
                Model.RenderControl(writer);
                Response.Write(sb.ToString());
                %>
    </div>
    <p>
    </p>
    <div style="padding: 5px; display: none" class="ui-state-error ui-corner-all"
      id="captcha-error">
      <p>
        <span style="float: left; margin-right: 0.3em;" class="ui-icon ui-icon-alert"></span>
        <strong>Error:</strong> looks like you typed in the wrong words - try again
      </p>
    </div>
    <input id="btn-captcha" style="font-weight: bold;" type="button" name="submit-captcha" value="&nbsp;I'm a Human Being&nbsp;" /><br>
</div>

