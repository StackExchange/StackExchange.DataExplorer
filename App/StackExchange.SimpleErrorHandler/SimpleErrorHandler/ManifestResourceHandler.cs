namespace SimpleErrorHandler
{

    using System;
    using System.Web;
    using System.IO;
    using Encoding = System.Text.Encoding;

    /// <summary>
    /// Reads a resource from the assembly manifest and returns its contents as the response entity.
    /// </summary>
    internal sealed class ManifestResourceHandler : IHttpHandler
    {
        private string _resourceName;
        private string _contentType;
        private Encoding _responseEncoding;

        public ManifestResourceHandler(string resourceName, string contentType) : this(resourceName, contentType, null) { }

        public ManifestResourceHandler(string resourceName, string contentType, Encoding responseEncoding)
        {
            _resourceName = resourceName;
            _contentType = contentType;
            _responseEncoding = responseEncoding;
        }

        public void ProcessRequest(HttpContext context)
        {
            // Grab the resource stream from the manifest.
            Type thisType = this.GetType();
            var output = new System.Text.StringBuilder();

            using (Stream stream = thisType.Assembly.GetManifestResourceStream(thisType, _resourceName))
            using (StringWriter sr = new StringWriter(output))
            {
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);

                char[] chars = Encoding.Default.GetChars(bytes);
                sr.Write(chars);
            }

            if (_responseEncoding != null) context.Response.ContentEncoding = _responseEncoding;
            context.Response.ContentType = _contentType;
            context.Response.Write(output.ToString());            
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}