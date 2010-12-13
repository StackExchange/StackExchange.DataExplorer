/*
 This file is derived off ELMAH:

http://code.google.com/p/elmah/

http://www.apache.org/licenses/LICENSE-2.0
 
 */

namespace SimpleErrorHandler
{
    using System;
    using System.Web;
    using System.Collections.Specialized;
    using System.Threading;
    using System.Xml;

    /// <summary>
    /// Represents a logical application error (as opposed to the actual exception it may be representing).
    /// </summary>
    [Serializable]
    public class Error : IXmlExportable, ICloneable
    {
        public string Id { get; set; }

        private Exception _exception;
        private string _applicationName;
        private string _hostName;
        private string _typeName;
        private string _source;
        private string _message;
        private string _detail;
        private string _user;
        private DateTime _time;
        private int _statusCode;
        private string _webHostHtmlMessage;
        private NameValueCollection _serverVariables;
        private NameValueCollection _queryString;
        private NameValueCollection _form;
        private NameValueCollection _cookies;

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class.
        /// </summary>
        public Error() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class from a given <see cref="Exception"/> instance.
        /// </summary>
        public Error(Exception e): this(e, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class
        /// from a given <see cref="Exception"/> instance and 
        /// <see cref="HttpContext"/> instance representing the HTTP 
        /// context during the exception.
        /// </summary>
        public Error(Exception e, HttpContext context)
        {
            if (e == null) throw new ArgumentNullException("e");

            _exception = e;
            Exception baseException = e.GetBaseException();

            _hostName = Environment.MachineName;
            _typeName = baseException.GetType().FullName;
            _message = baseException.Message;
            _source = baseException.Source;
            _detail = e.ToString();
            _user = Thread.CurrentPrincipal.Identity.Name ?? "";
            _time = DateTime.Now;

            HttpException httpException = e as HttpException;
            if (httpException != null)
            {
                _statusCode = httpException.GetHttpCode();
                _webHostHtmlMessage = httpException.GetHtmlErrorMessage() ?? "";
            }

            if (context != null)
            {
                HttpRequest request = context.Request;
                _serverVariables = CopyCollection(request.ServerVariables);
                _queryString = CopyCollection(request.QueryString);
                _form = CopyCollection(request.Form);
                _cookies = CopyCollection(request.Cookies);
            }
        }

        /// <summary>
        /// For XmlErrorLog only - reflects the xml file's "Read Only" file attribute.
        /// </summary>
        public bool IsProtected { get; set; }

        /// <summary>
        /// Get the <see cref="Exception"/> instance used to initialize this instance.
        /// </summary>
        /// <remarks>
        /// This is a run-time property only that is not written or read 
        /// during XML serialization via <see cref="FromXml"/> and 
        /// <see cref="ToXml"/>.
        /// </remarks>
        public Exception Exception
        {
            get { return _exception; }
        }

        /// <summary>
        /// Gets or sets the name of application in which this error occurred.
        /// </summary>
        public string ApplicationName
        {
            get { return _applicationName ?? ""; }
            set { _applicationName = value; }
        }

        /// <summary>
        /// Gets or sets name of host machine where this error occurred.
        /// </summary>
        public string HostName
        {
            get { return _hostName ?? ""; }
            set { _hostName = value; }
        }

        /// <summary>
        /// Get or sets the type, class or category of the error.
        /// </summary>
        public string Type
        {
            get { return _typeName ?? ""; }
            set { _typeName = value; }
        }

        /// <summary>
        /// Gets or sets the source that is the cause of the error.
        /// </summary>
        public string Source
        {
            get { return _source ?? ""; }
            set { _source = value; }
        }

        /// <summary>
        /// Gets or sets a brief text describing the error.
        /// </summary>
        public string Message
        {
            get { return _message ?? ""; }
            set { _message = value; }
        }

        /// <summary>
        /// Gets or sets a detailed text describing the error, such as a
        /// stack trace.
        /// </summary>
        public string Detail
        {
            get { return _detail ?? ""; }
            set { _detail = value; }
        }

        /// <summary>
        /// Gets or sets the user logged into the application at the time of the error.
        /// </summary>
        public string User
        {
            get { return _user ?? ""; }
            set { _user = value; }
        }

        /// <summary>
        /// Gets or sets the date and time (in local time) at which the error occurred.
        /// </summary>
        public DateTime Time
        {
            get { return _time; }
            set { _time = value; }
        }

        /// <summary>
        /// Gets or sets the HTTP status code of the output returned to the client for the error.
        /// </summary>
        public int StatusCode
        {
            get { return _statusCode; }
            set { _statusCode = value; }
        }

        /// <summary>
        /// Gets or sets the HTML message generated by the web host (ASP.NET) for the given error.
        /// </summary>
        public string WebHostHtmlMessage
        {
            get { return _webHostHtmlMessage ?? ""; }
            set { _webHostHtmlMessage = value; }
        }

        /// <summary>
        /// Gets a collection representing the Web server variables captured as part of diagnostic data for the error.
        /// </summary>
        public NameValueCollection ServerVariables
        {
            get { return FaultIn(ref _serverVariables); }
        }

        /// <summary>
        /// Gets a collection representing the Web query string variables captured as part of diagnostic data for the error.
        /// </summary>

        public NameValueCollection QueryString
        {
            get { return FaultIn(ref _queryString); }
        }

        /// <summary>
        /// Gets a collection representing the form variables captured as part of diagnostic data for the error.
        /// </summary>
        public NameValueCollection Form
        {
            get { return FaultIn(ref _form); }
        }

        /// <summary>
        /// Gets a collection representing the client cookies captured as part of diagnostic data for the error.
        /// </summary>
        public NameValueCollection Cookies
        {
            get { return FaultIn(ref _cookies); }
        }

        /// <summary>
        /// The number of newer Errors that have been discarded because they match this Error and fall within the configured 
        /// "IgnoreSimilarExceptionsThreshold" TimeSpan value.
        /// </summary>
        public int? DuplicateCount { get; set; }

        /// <summary>
        /// Returns the value of the <see cref="Message"/> property.
        /// </summary>
        public override string ToString()
        {
            return this.Message;
        }

        /// <summary>
        /// Loads the error object from its XML representation.
        /// </summary>
        public void FromXml(XmlReader r)
        {
            if (!r.IsStartElement()) throw new ArgumentOutOfRangeException("reader");

            ReadXmlAttributes(r);
            bool isEmpty = r.IsEmptyElement;
            r.Read();
            if (!isEmpty)
            {
                ReadInnerXml(r);
                r.ReadEndElement();
            }
        }

        /// <summary>
        /// Reads the error data in XML attributes.
        /// </summary>
        protected virtual void ReadXmlAttributes(XmlReader r)
        {
            if (!r.IsStartElement()) throw new ArgumentOutOfRangeException("reader");

            string id = r.GetAttribute("errorId");
            if (id.HasValue())
                Id = id;

            _applicationName = r.GetAttribute("application");
            _hostName = r.GetAttribute("host");
            _typeName = r.GetAttribute("type");
            _message = r.GetAttribute("message");
            _source = r.GetAttribute("source");
            _detail = r.GetAttribute("detail");
            _user = r.GetAttribute("user");
            string timeString = r.GetAttribute("time") ?? "";
            _time = timeString.Length == 0 ? new DateTime() : XmlConvert.ToDateTime(timeString, System.Xml.XmlDateTimeSerializationMode.Local);
            string statusCodeString = r.GetAttribute("statusCode") ?? "";
            _statusCode = statusCodeString.Length == 0 ? 0 : XmlConvert.ToInt32(statusCodeString);
            _webHostHtmlMessage = r.GetAttribute("webHostHtmlMessage");

            var dupCount = r.GetAttribute("duplicateCount");
            if (dupCount.HasValue())
                DuplicateCount = int.Parse(dupCount);
        }

        /// <summary>
        /// Reads the error data in child nodes.
        /// </summary>
        protected virtual void ReadInnerXml(XmlReader r)
        {
            //
            // Loop through the elements, reading those that we
            // recognize. If an unknown element is found then
            // this method bails out immediately without
            // consuming it, assuming that it belongs to a subclass.
            //
            while (r.IsStartElement())
            {
                NameValueCollection collection;
                switch (r.LocalName)
                {
                    case "serverVariables": collection = this.ServerVariables; break;
                    case "queryString": collection = this.QueryString; break;
                    case "form": collection = this.Form; break;
                    case "cookies": collection = this.Cookies; break;
                    default: return;
                }

                if (r.IsEmptyElement)
                {
                    r.Read();
                }
                else
                {
                    ((IXmlExportable)collection).FromXml(r);
                }
            }
        }

        /// <summary>
        /// Writes the error data to its XML representation.
        /// </summary>
        public void ToXml(XmlWriter w)
        {
            if (w.WriteState != WriteState.Element) throw new ArgumentOutOfRangeException("writer");
            // Write out the basic typed information in attributes followed by collections as inner elements.
            WriteXmlAttributes(w);
            WriteInnerXml(w);
        }

        /// <summary>
        /// Writes the error data that belongs in XML attributes.
        /// </summary>
        protected virtual void WriteXmlAttributes(XmlWriter w)
        {
            if (Id.HasValue())
                WriteXmlAttribute(w, "errorId", Id);

            WriteXmlAttribute(w, "application", _applicationName);
            WriteXmlAttribute(w, "host", _hostName);
            WriteXmlAttribute(w, "type", _typeName);
            WriteXmlAttribute(w, "message", _message);
            WriteXmlAttribute(w, "source", _source);
            WriteXmlAttribute(w, "detail", _detail);
            WriteXmlAttribute(w, "user", _user);
            if (_time != DateTime.MinValue)
                WriteXmlAttribute(w, "time", XmlConvert.ToString(_time, XmlDateTimeSerializationMode.Local));
            if (_statusCode != 0)
                WriteXmlAttribute(w, "statusCode", XmlConvert.ToString(_statusCode));
            WriteXmlAttribute(w, "webHostHtmlMessage", _webHostHtmlMessage);

            if (DuplicateCount.HasValue)
                WriteXmlAttribute(w, "duplicateCount", DuplicateCount.ToString());
        }

        /// <summary>
        /// Writes the error data that belongs in child nodes.
        /// </summary>
        protected virtual void WriteInnerXml(XmlWriter w)
        {
            WriteCollection(w, "serverVariables", _serverVariables);
            WriteCollection(w, "queryString", _queryString);
            WriteCollection(w, "form", _form);
            WriteCollection(w, "cookies", _cookies);
        }

        private void WriteCollection(XmlWriter w, string name, NameValueCollection collection)
        {
            if (collection != null && collection.Count != 0)
            {
                w.WriteStartElement(name);
                ((IXmlExportable)collection).ToXml(w);
                w.WriteEndElement();
            }
        }

        private void WriteXmlAttribute(XmlWriter w, string name, string value)
        {
            if (!String.IsNullOrEmpty(value)) w.WriteAttributeString(name, value);
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        object ICloneable.Clone()
        {
            // Make a base shallow copy of all the members.
            Error copy = (Error)MemberwiseClone();

            // Now make a deep copy of items that are mutable.
            copy._serverVariables = CopyCollection(_serverVariables);
            copy._queryString = CopyCollection(_queryString);
            copy._form = CopyCollection(_form);
            copy._cookies = CopyCollection(_cookies);

            return copy;
        }

        private NameValueCollection CopyCollection(NameValueCollection collection)
        {
            if (collection == null || collection.Count == 0) return null;
            return new HttpValuesCollection(collection);
        }

        private NameValueCollection CopyCollection(HttpCookieCollection cookies)
        {
            if (cookies == null || cookies.Count == 0) return null;

            NameValueCollection copy = new HttpValuesCollection(cookies.Count);
            for (int i = 0; i < cookies.Count; i++)
            {
                HttpCookie cookie = cookies[i];
                // NOTE: We drop the Path and Domain properties of the cookie for sake of simplicity.
                copy.Add(cookie.Name, cookie.Value);
            }

            return copy;
        }

        private static NameValueCollection FaultIn(ref NameValueCollection collection)
        {
            if (collection == null) collection = new HttpValuesCollection();
            return collection;
        }
    }
}