namespace SimpleErrorHandler
{
	using System;
    using System.Web;
    using ContentSyndication;
    using XmlSerializer = System.Xml.Serialization.XmlSerializer;
    using ArrayList = System.Collections.ArrayList;

    /// <summary>
    /// Renders a XML using the RSS 0.91 vocabulary that displays, at most,
    /// the 15 most recent errors recorded in the error log.
    /// </summary>
    internal sealed class ErrorRssHandler : IHttpHandler
	{
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/xml";

            // Get the last set of errors for this application.
            const int pageSize = 15;
            ArrayList errorEntryList = new ArrayList(pageSize);
            ErrorLog.Default.GetErrors(0, pageSize, errorEntryList);

            // We'll be emitting RSS vesion 0.91.
            RichSiteSummary rss = new RichSiteSummary();
            rss.version = "0.91";

            // Set up the RSS channel.
            Channel channel = new Channel();
            channel.title = "Error log of " + ErrorLog.Default.ApplicationName + " on " + Environment.MachineName;
            channel.description = "Log of recent errors";
            channel.language = "en";
            channel.link = context.Request.Url.GetLeftPart(UriPartial.Authority) + 
                context.Request.ServerVariables["URL"];
            rss.channel = channel;

            // For each error, build a simple channel item. 
            // Only the title, description, link and pubDate fields are populated.
            channel.item = new Item[errorEntryList.Count];

            for (int index = 0; index < errorEntryList.Count; index++)
            {
                ErrorLogEntry errorEntry = (ErrorLogEntry) errorEntryList[index];
                Error error = errorEntry.Error;

                Item item = new Item();

                item.title = error.Message;
                item.description = "An error of type " + error.Type + " occurred. " + error.Message;
                item.link = channel.link + "/detail?id=" + errorEntry.Id;
                item.pubDate = error.Time.ToUniversalTime().ToString("r");

                channel.item[index] = item;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(RichSiteSummary));
            serializer.Serialize(context.Response.Output, rss);
        }

        public bool IsReusable
        {
            get { return false; }
        }
    }
}