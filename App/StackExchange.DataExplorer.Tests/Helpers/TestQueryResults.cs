using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StackExchange.DataExplorer.Tests.Helpers {
    [TestClass]
    public class TestQueryResults {

        [TestMethod]
        public void TestJsonPersistance() {
            QueryResults results = MockResults();

            var data = JObject.Parse(results.ToJson());
            Assert.AreEqual("test", data["url"]);
            Assert.AreEqual(100, data["queryId"]);
            Assert.AreEqual("hello", data["resultSets"][0]["rows"][0][0]);
        }

        [TestMethod]
        public void TestDeserialization() {
            QueryResults results = MockResults();
            var other = QueryResults.FromJson(results.ToJson());

            Assert.AreEqual(results.Url, other.Url);
            Assert.AreEqual(results.QueryId, other.QueryId);
            Assert.AreEqual(results.ResultSets[0].Rows[0][0], other.ResultSets[0].Rows[0][0]);
        }

        [TestMethod]
        public void TestToText() {
            QueryResults results = new QueryResults();
            results.Messages = 
@"1

2

";

            ResultSet first = new ResultSet();
            first.MessagePosition = 0;
            first.Columns.Add(new ResultColumnInfo() {Name =  "a"});
            first.Rows.Add(new List<object>() { "xxx" });

            ResultSet second = new ResultSet();
            second.MessagePosition = 4;
            second.Columns.Add(new ResultColumnInfo() {Name =  "hello"});
            second.Rows.Add(new List<object>() { "x" });

            results.ResultSets.Add(first);
            results.ResultSets.Add(second);

            var transformed = results.ToTextResults(); 

            Assert.AreEqual(true, transformed.TextOnly);

            var expected = @"a
---
xxx

1
hello
-----
x


2

";

            var actual = string.Join("\r\n", transformed.Messages.Split('\n').Select(s => s.Trim()));


            Assert.AreEqual(expected
 , actual);
    
        }

        private static QueryResults MockResults() {
            var rows = new List<List<object>>();
            rows.Add(new List<object>());
            rows[0].Add("hello");

            var resultSet = new ResultSet();
            resultSet.Rows = rows;

            QueryResults results = new QueryResults()
            {
                Url = "test",
                QueryId = 100
            };

            results.ResultSets.Add(resultSet);

            return results;
        }
    }
}
