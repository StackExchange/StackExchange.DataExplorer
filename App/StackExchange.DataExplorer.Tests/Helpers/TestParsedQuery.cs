using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;
using System.Collections.Specialized;

namespace StackExchange.DataExplorer.Tests.Helpers {
    [TestClass]
    public class TestParsedQuery {
        [TestMethod]
        public void TestSimpleParsing()
        {
            string sql = "SELECT TOP 10 * FROM Posts";

            var query = new ParsedQuery(sql, null);

            Assert.AreEqual(sql, query.Sql);
            Assert.AreEqual(sql, query.ExecutionSql);
        }

        [TestMethod]
        public void TestSimpleReductionParsing()
        {
            string sql = new StringBuilder()
                .Append("SELECT\n")
                .Append("  TOP 10 *\n")
                .Append("FROM\n")
                .Append("  Posts")
                .ToString();

            var query = new ParsedQuery(sql, null);

            Assert.AreEqual(sql, query.Sql);
            Assert.AreEqual("SELECT TOP 10 * FROM Posts", query.ExecutionSql);
        }

        [TestMethod]
        public void TestBatchSplitting() {
            var query = new ParsedQuery("select 1\n  Go  \nselect 2\n1", null);
            var batches = query.ExecutionSqlBatches.ToArray();
            Assert.AreEqual(2, batches.Length);
            Assert.AreEqual("select 1\n", batches[0]);
            Assert.AreEqual("\nselect 2\n1", batches[1]);
        }

        [TestMethod]
        public void TestBatchSplittingIgnoresComments() {
            var query = new ParsedQuery("select 1\n--Go\nselect 2\n1", null);
            var batches = query.ExecutionSqlBatches.ToArray();
            Assert.AreEqual(1, batches.Length);
            Assert.AreEqual("select 1\n--Go\nselect 2\n1", batches[0]);
       
        }

        [TestMethod]
        public void TestBatchSplittingIgnoresEmptyBatches() {
            var query = new ParsedQuery("select 1\nGo", null);
            var batches = query.ExecutionSqlBatches.ToArray();
            Assert.AreEqual(1, batches.Length);
            Assert.AreEqual("select 1\n", batches[0]);

        }

        
        [TestMethod]
        public void TestNameCommentIsPulledIntoName() {
            var query = new ParsedQuery("-- hello world \nselect 1", null);
            Assert.AreEqual("hello world", query.Name);
        }

        [TestMethod]
        public void TestNameCommentIsPulledIntoDescription() {
            var sb = new StringBuilder()
                .AppendLine("--test")
                .AppendLine("--desc1")
                .AppendLine("--desc2")
                .AppendLine("select 1");

            var query = new ParsedQuery(sb.ToString(), null);
            Assert.AreEqual("desc1\ndesc2", query.Description);
        }

        [TestMethod]
        public void TestRawSqlIsNormalized() {
            var sb = new StringBuilder()
              .AppendLine("")
              .AppendLine(" select 2 ")
              .AppendLine(" -- select 1 ")
              .AppendLine("");

            var query = new ParsedQuery(sb.ToString(), null);
            Assert.AreEqual(" select 2 \n -- select 1 ", query.Sql);
        }

        [TestMethod]
        public void TestSqlStripsComments() {
            var sb = new StringBuilder()
              .AppendLine("--")
              .AppendLine("--")
              .AppendLine(" select 2 ")
              .AppendLine(" -- select 1 ")
              .AppendLine("");

            var query = new ParsedQuery(sb.ToString(), null);
            Assert.AreEqual(" select 2 \n -- select 1 ", query.Sql);
        }

        [TestMethod]
        public void TestWeDetectMissingVars() {
            var sb = new StringBuilder()
                .AppendLine("##a## ##b##");

            var collection = new NameValueCollection();
            collection.Add("a", "1");
            collection.Add("b", "");

            var query = new ParsedQuery(sb.ToString(), collection);
            Assert.IsFalse(query.AllParamsSet);
        }

        [TestMethod]
        public void TestWeDetectAllParams() {

            var collection = new NameValueCollection();
            collection.Add("a", "1");
            collection.Add("b", "3");

            var query = new ParsedQuery("##a## ##b##", collection);
            Assert.IsTrue(query.AllParamsSet);
        }

        [TestMethod]
        public void TestIntParams()
        {
            var collection = new NameValueCollection();
            collection.Add("a", "1");
            collection.Add("b", "3");

            var query = new ParsedQuery("##a:int## ##b##", collection);
            Assert.IsTrue(query.AllParamsSet);
        }

        [TestMethod]
        public void TestInvalidIntParams() {
            var collection = new NameValueCollection();
            collection.Add("a", "hello");
            collection.Add("b", "3");

            var query = new ParsedQuery("##a:int## ##b##", collection);
            Assert.IsFalse(query.AllParamsSet);
            Assert.AreEqual("Expected a to be an int!", query.ErrorMessage);
        }

        [TestMethod]
        public void TestInvalidParamType()
        {
            var collection = new NameValueCollection();
            collection.Add("a", "hello");

            var query = new ParsedQuery("##a:frog##", collection);
            Assert.AreEqual("Unknown parameter type frog!", query.ErrorMessage);
        }

        [TestMethod]
        public void TestStringEncoding()
        {
            var collection = new NameValueCollection();
            collection.Add("a", "I'm");

            var query = new ParsedQuery("##a:string##", collection);
            Assert.AreEqual("'I''m'", query.ExecutionSql);
        }

        [TestMethod]
        public void TestInvalidFloats() {
            var collection = new NameValueCollection();
            collection.Add("a", "1.");
            collection.Add("b", "1.2.");
            collection.Add("c", ".2");
            collection.Add("d", "frog");

            var query = new ParsedQuery("##a:float## ##b:float## ##c:float## ##d:float##", collection);
            Assert.AreEqual(@"Expected a to be of type float!
Expected b to be of type float!
Expected c to be of type float!
Expected d to be of type float!".Replace("\r",""), query.ErrorMessage);
        }

        [TestMethod]
        public void TestValidFloats() {
            var collection = new NameValueCollection();
            collection.Add("a", "1");
            collection.Add("b", "1.2");

            var query = new ParsedQuery("##a:float## ##b:float##", collection);
            Assert.IsTrue(query.AllParamsSet);
            Assert.AreEqual(query.ExecutionSql, "1 1.2");
        }
    }
}
