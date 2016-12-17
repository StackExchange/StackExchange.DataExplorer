using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Tests.Models
{
    [TestClass]
    public class TestQueryExecutions : BaseTest
    {
        [TestMethod]
        public void TestBatchingBatch()
        {
            string sql = "print 1 \nGO\nprint 2";
            var site = Current.DB.Sites.First();
            var user = User.CreateUser("Fred", "a@a.com", "xyzdsa");
            var results = QueryRunner.ExecuteNonCached(new ParsedQuery(sql, null), site, user, null);

            Assert.AreEqual(0, results.ResultSets.Count());
            Assert.AreEqual("1\r\n2\r\n", results.Messages);
        }

        [TestMethod]
        public void TestMultiResultSetsInStatement()
        {
            string sql = "select 1 select 2";
            var site = Current.DB.Sites.First();
            var user = User.CreateUser("Fred", "a@a.com", "xyzdsa");
            var results = QueryRunner.ExecuteNonCached(new ParsedQuery(sql, null), site, user, null);

            Assert.AreEqual(2, results.ResultSets.Count());
        }

        [TestMethod]
        public void TestBasicExecution()
        {
            /*
            string sql = "select top 10 Id as [Post Link] from Posts";
            var site = Current.DB.Sites.First();

            var user = User.CreateUser("Fred", "a@a.com", "xyzdsa");
            var results = QueryRunner.ExecuteNonCached(new ParsedQuery(sql, null), site, user);
            QueryRunner.LogQueryExecution(user, site , new ParsedQuery(sql,null), results);

            var executions = Current.DB.QueryExecutions.Count(q => q.UserId == user.Id && q.QueryId == results.QueryId);
            Assert.AreEqual(1, executions);
            */
        }

        [TestMethod]
        public void TestRepeatExecutions()
        {
            /*
            string sql = "select top 10 Id as [Post Link] from Posts";
            var site = Current.DB.Sites.First();

            var user = User.CreateUser("Fred", "a@a.com", "xyzdsa");
            var results = QueryRunner.ExecuteNonCached(new ParsedQuery(sql, null), site, user);
            QueryRunner.LogQueryExecution(user, site, new ParsedQuery(sql, null), results);
            QueryRunner.LogQueryExecution(user, site, new ParsedQuery(sql, null), results);

            var runs = Current.DB.QueryExecutions.Where(q => q.UserId == user.Id && q.QueryId == results.QueryId);
           
            Assert.AreEqual(1, runs.Count());
            Assert.AreEqual(2, runs.First().ExecutionCount);
            */
        }
    }
}
