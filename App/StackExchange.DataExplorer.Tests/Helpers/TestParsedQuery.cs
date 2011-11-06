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

            Assert.AreEqual(sql, query.ExecutionSql);
        }

        [TestMethod]
        public void TestSimpleReductionParsing()
        {
            string sql = new StringBuilder()
                .AppendLine("SELECT")
                .AppendLine("  TOP 10 *")
                .AppendLine("FROM")
                .AppendLine("  Posts")
                .ToString();

            var query = new ParsedQuery(sql, null);

            Assert.AreEqual("SELECT TOP 10 * FROM Posts", query.ExecutionSql);
        }

        [TestMethod]
        public void TestCommentReductionParsing()
        {
            string sql = new StringBuilder()
                .AppendLine("-- A single line comment")
                .AppendLine("SELECT")
                .AppendLine("  TOP 10 * -- We only want the top 10")
                .AppendLine("FROM")
                .AppendLine("/* Posts */")
                .AppendLine("/*")
                .AppendLine("  Comments")
                .AppendLine(" */")
                .AppendLine("  Users")
                .ToString();

            var query = new ParsedQuery(sql, null);

            Assert.AreEqual("SELECT TOP 10 * FROM Users", query.ExecutionSql);
        }

        [TestMethod]
        public void TestMultiLineStringReductionParsing()
        {
            string sql = new StringBuilder()
                .AppendLine("SELECT TOP 10 * FROM Posts WHERE Body LIKE '%")
                .AppendLine("   }%'")
                .AppendLine("WHERE")
                .AppendLine("  Id > 10")
                .ToString();

            var query = new ParsedQuery(sql, null);

            Assert.AreEqual("SELECT TOP 10 * FROM Posts WHERE Body LIKE '%\n   }%' WHERE Id > 10", query.ExecutionSql);
        }

        [TestMethod]
        public void TestBatchSplitting() {
            string sql = new StringBuilder()
                .AppendLine("SELECT 1")
                .AppendLine("  GO  ")
                .AppendLine("SELECT 2")
                .AppendLine("1")
                .ToString();

            var query = new ParsedQuery(sql, null);
            var batches = query.ExecutionSqlBatches.ToArray();

            Assert.AreEqual(2, batches.Length);
            Assert.AreEqual("SELECT 1", batches[0]);
            Assert.AreEqual("SELECT 2 1", batches[1]);
        }

        [TestMethod]
        public void TestBatchSplittingIgnoresComments() {
            string sql = new StringBuilder()
                .AppendLine("SELECT 1")
                .AppendLine("--Go")
                .AppendLine("SELECT 2")
                .AppendLine("1")
                .ToString();

            var query = new ParsedQuery(sql, null);
            var batches = query.ExecutionSqlBatches.ToArray();

            Assert.AreEqual(1, batches.Length);
            Assert.AreEqual("SELECT 1\nSELECT 2 1", batches[0]);
       
        }

        [TestMethod]
        public void TestBatchSplittingIgnoresEmptyBatches() {
            string sql = new StringBuilder()
                .AppendLine("SELECT 1")
                .AppendLine("GO")
                .ToString();

            var query = new ParsedQuery(sql, null);
            var batches = query.ExecutionSqlBatches.ToArray();

            Assert.AreEqual(1, batches.Length);
            Assert.AreEqual("SELECT 1", batches[0]);
        }

        [TestMethod]
        public void TestWeDetectMissingParameterValues() {
            string sql = "##a## ##b##";

            var parameters = new NameValueCollection
            {
                { "a", "1" },
                { "b", "" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.IsFalse(query.IsExecutionReady);
            Assert.AreEqual("Missing value for b!", query.Errors[0]);
        }

        [TestMethod]
        public void TestWeDetectAllParameters() {
            string sql = "##a## ##b##";

            var parameters = new NameValueCollection
            {
                { "a", "1" },
                { "b", "3" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.IsTrue(query.Parameters.ContainsKey("a"));
            Assert.IsTrue(query.Parameters.ContainsKey("b"));
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestWeIgnoreCommentedParameters()
        {
            string sql = new StringBuilder()
                .AppendLine("SELECT")
                .AppendLine("  TOP ##TopCount##")
                .AppendLine("FROM")
                .AppendLine("  Posts")
                .AppendLine("--WHERE UserId == ##UserId##")
                .ToString();

            var parameters = new NameValueCollection
            {
                { "TopCount", "10" },
                { "UserId", "1" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.IsTrue(query.Parameters.ContainsKey("TopCount"));
            Assert.IsFalse(query.Parameters.ContainsKey("UserId"));
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestWeParseParametersInMultiLineStrings()
        {
            string sql = new StringBuilder()
                .AppendLine("SELECT * FROM Posts WHERE Body LIKE '%")
                .AppendLine("##SearchTerm##'")
                .ToString();

            var parameters = new NameValueCollection
            {
                { "SearchTerm", "foobar" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.IsTrue(query.Parameters.ContainsKey("SearchTerm"));
            Assert.AreEqual("SELECT * FROM Posts WHERE Body LIKE '%\nfoobar'", query.ExecutionSql);
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestInvalidParameterType()
        {
            string sql = "##a:frog##";

            var parameters = new NameValueCollection
            {
                { "a", "thingadongdong" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.IsFalse(query.Parameters.ContainsKey("a"));
            Assert.AreEqual("a has unknown parameter type frog!", query.ErrorMessage);
            Assert.IsFalse(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestIntParameters()
        {
            string sql = "##a:int## ##b##";

            var parameters = new NameValueCollection
            {
                { "a", "1" },
                { "b", "3" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.AreEqual("int", query.Parameters["a"].Type);
            Assert.AreEqual("1 3", query.ExecutionSql);
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestInvalidIntParameters() {
            string sql = "##a:int## ##b##";

            var parameters = new NameValueCollection
            {
                { "a", "hello" },
                { "b", "3" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.AreEqual("Expected value of a to be a int!", query.ErrorMessage);
            Assert.IsFalse(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestStringEncoding()
        {
            string sql = "SELECT * FROM Users WHERE Login = ##UserName:string##";

            var parameters = new NameValueCollection
            {
                { "UserName", "I'm a User's Name" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.AreEqual("SELECT * FROM Users WHERE Login = 'I''m a User''s Name'", query.ExecutionSql);
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestValidFloats()
        {
            string sql = "##a:float## ##b:float##";

            var parameters = new NameValueCollection
            {
                { "a", "1" },
                { "b", "1.2" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.AreEqual("float", query.Parameters["a"].Type);
            Assert.AreEqual(query.ExecutionSql, "1 1.2");
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestInvalidFloats()
        {
            string sql = "##a:float## ##b:float## ##c:float## ##d:float##";

            var parameters = new NameValueCollection
            {
                { "a", "1." },
                { "b", "1.2." },
                { "c", ".2" },
                { "d", "frog" }
            };

            var query = new ParsedQuery(sql, parameters);

            Assert.AreEqual(4, query.Errors.Count);
            Assert.AreEqual("Expected value of a to be a float!", query.Errors[0]);
            Assert.AreEqual("Expected value of b to be a float!", query.Errors[1]);
            Assert.AreEqual("Expected value of c to be a float!", query.Errors[2]);
            Assert.AreEqual("Expected value of d to be a float!", query.Errors[3]);
            Assert.IsFalse(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestDefaultParameterValue()
        {
            string sql = "SELECT * FROM Tags WHERE TagName = '##TagName?java##'";

            var query = new ParsedQuery(sql, null);

            Assert.IsTrue(query.Parameters.ContainsKey("TagName"));
            Assert.AreEqual("java", query.Parameters["TagName"].Default);
            Assert.AreEqual("SELECT * FROM Tags WHERE TagName = 'java'", query.ExecutionSql);
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestTypedDefaultParameterValue()
        {
            string sql = "SELECT * FROM Users WHERE Reputation > ##Reputation:int?101##";

            var query = new ParsedQuery(sql, null);

            Assert.IsTrue(query.Parameters.ContainsKey("Reputation"));
            Assert.AreEqual("101", query.Parameters["Reputation"].Default);
            Assert.AreEqual("SELECT * FROM Users WHERE Reputation > 101", query.ExecutionSql);
            Assert.IsTrue(query.IsExecutionReady);
        }

        [TestMethod]
        public void TestInvalidTypedDefaultParameterValue()
        {
            string sql = "SELECT * FROM Users WHERE Reputation > ##Reputation:int?trees##";

            var query = new ParsedQuery(sql, null);

            Assert.IsTrue(query.Parameters.ContainsKey("Reputation"));
            Assert.AreEqual(1, query.Errors.Count);
            Assert.AreEqual("Expected default value trees for Reputation to be a int!", query.Errors[0]);
            Assert.IsFalse(query.IsExecutionReady);
        }
    }
}
