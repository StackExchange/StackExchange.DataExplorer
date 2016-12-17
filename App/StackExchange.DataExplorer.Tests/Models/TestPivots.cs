using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Tests.Models
{
    [TestClass]
    public class TestPivots : BaseTest
    {
        [TestMethod]
        public void TestBasicMerging()
        {
            var results1 = new QueryResults();
            var results2 = new QueryResults();

            results1.ResultSets = new List<ResultSet> { new ResultSet() };
            results2.ResultSets = new List<ResultSet> { new ResultSet() };

            results1.ResultSets[0].Columns = new List<ResultColumnInfo>
            {
                new ResultColumnInfo{ Name = "col1", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "col2", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "Pivot", Type = ResultColumnType.Default }
            };

            results2.ResultSets[0].Columns = new List<ResultColumnInfo>
            {
                new ResultColumnInfo{ Name = "col1", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "col2", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "Pivot", Type = ResultColumnType.Default }
            };

            results1.ResultSets[0].Rows = new List<List<object>>
            {
                new List<object>{1,1,1},
                new List<object>{2,2,2},
                new List<object>{3,3,3},
            };

            results2.ResultSets[0].Rows = new List<List<object>>
            {
                new List<object>{2,2,99}
            };

            QueryRunner.MergePivot(Current.DB.Sites.First(), results1, results2);


            Assert.IsNull(results1.ResultSets[0].Rows[0][3]);
            Assert.AreEqual(99, results1.ResultSets[0].Rows[1][3]);
            Assert.IsNull(results1.ResultSets[0].Rows[2][3]);
        }


        [TestMethod]
        public void TestGapsInFirstResultSet()
        {
            var results1 = new QueryResults();
            var results2 = new QueryResults();

            results1.ResultSets = new List<ResultSet> { new ResultSet() };
            results2.ResultSets = new List<ResultSet> { new ResultSet() };

            results1.ResultSets[0].Columns = new List<ResultColumnInfo>
            {
                new ResultColumnInfo{ Name = "col1", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "col2", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "Pivot", Type = ResultColumnType.Default }
            };

            results2.ResultSets[0].Columns = new List<ResultColumnInfo>
            {
                new ResultColumnInfo{ Name = "col1", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "col2", Type = ResultColumnType.Default },
                new ResultColumnInfo{ Name = "Pivot", Type = ResultColumnType.Default }
            };

            results1.ResultSets[0].Rows = new List<List<object>>
            {
                new List<object>{1,1,1},
                new List<object>{2,2,2},
                new List<object>{3,3,3},
            };

            results2.ResultSets[0].Rows = new List<List<object>>
            {
                new List<object>{2,2,99},
                new List<object>{4,4,666}
            };

            QueryRunner.MergePivot(Current.DB.Sites.First(), results1, results2);


            Assert.IsNull(results1.ResultSets[0].Rows[0][3]);
            Assert.AreEqual(99, results1.ResultSets[0].Rows[1][3]);
            Assert.IsNull(results1.ResultSets[0].Rows[2][3]);

            Assert.IsNull(results1.ResultSets[0].Rows[3][2]);
            Assert.AreEqual(666, results1.ResultSets[0].Rows[3][3]);
        }
    }
}
