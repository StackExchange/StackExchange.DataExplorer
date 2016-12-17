using System.Xml;

namespace StackExchange.DataExplorer.Helpers
{
    /// <summary>
    /// Helper class for combining many query execution plans into one plan document.
    /// </summary>
    public class QueryPlan
    {
        private XmlDocument _planDocument;
        /// <summary>
        /// Gets the complete plan Xml, as a string.
        /// </summary>
        public string PlanXml => _planDocument?.OuterXml;

        /// <summary>
        /// Appends an xml query execution plan statement to the result plan document.
        /// </summary>
        /// <param name="xml">Xml query execution plan statement to add.</param>
        /// <remarks>
        /// You should call this method when combining many statements executed in a single batch 
        /// (for example multiple statements executed by a single SqlCommand).
        /// This method will append all statement plans found to the last batch found in the document.
        /// </remarks>
        public void AppendStatementPlan(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (_planDocument == null)
            {
                _planDocument = doc;
                return;
            }

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("s", "http://schemas.microsoft.com/sqlserver/2004/07/showplan");

            var allBatches = _planDocument.SelectNodes("s:ShowPlanXML/s:BatchSequence/s:Batch/s:Statements", nsManager);
            if (allBatches.Count == 0)
            {
                _planDocument = doc;
                return;
            }
            var batch = allBatches[allBatches.Count - 1];

            var statements = doc.SelectNodes("s:ShowPlanXML/s:BatchSequence/s:Batch/s:Statements/*", nsManager);
            foreach (XmlElement statement in statements)
            {
                var importedStatement = batch.OwnerDocument.ImportNode(statement, true);
                batch.AppendChild(importedStatement);
            }
        }

        /// <summary>
        /// Appends an xml query execution plan batch to the result plan document.
        /// </summary>
        /// <param name="xml">Xml query execution plan batch to add.</param>
        /// <remarks>
        /// You should call this method when combining many batches, for example queries executed by difference
        /// SqlCommand instances.
        /// </remarks>
        public void AppendBatchPlan(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return;
            }

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (_planDocument == null)
            {
                _planDocument = doc;
                return;
            }

            var nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("s", "http://schemas.microsoft.com/sqlserver/2004/07/showplan");

            var batchSequence = _planDocument.SelectSingleNode("s:ShowPlanXML/s:BatchSequence", nsManager);
            if (batchSequence == null)
            {
                _planDocument = doc;
                return;
            }

            var batches = doc.SelectNodes("s:ShowPlanXML/s:BatchSequence/*", nsManager);
            foreach (XmlElement batch in batches)
            {
                var importedBatch = batchSequence.OwnerDocument.ImportNode(batch, true);
                batchSequence.AppendChild(importedBatch);
            }
        }
    }
}