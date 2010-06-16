namespace SimpleErrorHandler
{
    using System;
    using System.Web;

    internal sealed class MessageHandler : IHttpHandler
    {
        private string _message;

        public MessageHandler(string message)
        {
            _message = message;
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/html";
            context.Response.Write(_message);
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}