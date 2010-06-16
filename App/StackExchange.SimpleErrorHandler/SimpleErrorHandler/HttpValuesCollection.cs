namespace SimpleErrorHandler
{
    using System;
    using NameValueCollection = System.Collections.Specialized.NameValueCollection;
    using XmlReader = System.Xml.XmlReader;
    using XmlWriter = System.Xml.XmlWriter;
    using SerializationInfo = System.Runtime.Serialization.SerializationInfo;
    using StreamingContext = System.Runtime.Serialization.StreamingContext;

    /// <summary>
    /// A name-values collection implementation suitable for web-based collections 
    /// (like server variables, query strings, forms and cookies) that can also
    /// be written and read as XML.
    /// </summary>
    [Serializable]
    internal sealed class HttpValuesCollection : NameValueCollection, IXmlExportable
    {
        public HttpValuesCollection() { }

        public HttpValuesCollection(NameValueCollection other) : base(other) { }

        public HttpValuesCollection(int capacity) : base(capacity) { }

        public HttpValuesCollection(int capacity, NameValueCollection other) : base(capacity, other) { }

        private HttpValuesCollection(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        void IXmlExportable.FromXml(XmlReader r)
        {
            if (r == null) throw new ArgumentNullException("reader");
            if (this.IsReadOnly) throw new InvalidOperationException("Object is read-only.");

            r.Read();

            // Add entries into the collection as <item> elements
            // with child <value> elements are found.
            while (r.IsStartElement("item"))
            {
                string name = r.GetAttribute("name");
                bool isNull = r.IsEmptyElement;

                r.Read(); // <item>

                if (!isNull)
                {

                    while (r.IsStartElement("value")) // <value ...>
                    {
                        string value = r.GetAttribute("string");
                        Add(name, value);
                        r.Read();
                    }

                    r.ReadEndElement(); // </item>
                }
                else
                {
                    Add(name, null);
                }
            }

            r.ReadEndElement();
        }

        void IXmlExportable.ToXml(XmlWriter w)
        {
            if (this.Count == 0)
            {
                return;
            }

            //
            // Write out a named multi-value collection as follows 
            // (example here is the ServerVariables collection):
            //
            //      <item name="HTTP_URL">
            //          <value string="/myapp/somewhere/page.aspx" />
            //      </item>
            //      <item name="QUERY_STRING">
            //          <value string="a=1&amp;b=2" />
            //      </item>
            //      ...
            //

            foreach (string key in this.Keys)
            {
                w.WriteStartElement("item");
                w.WriteAttributeString("name", key);

                string[] values = GetValues(key);

                if (values != null)
                {
                    foreach (string value in values)
                    {
                        w.WriteStartElement("value");
                        w.WriteAttributeString("string", value);
                        w.WriteEndElement();
                    }
                }

                w.WriteEndElement();
            }
        }
    }
}