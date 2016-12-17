using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Tests.Helpers
{
    [TestClass]
    public class TestQueryRunner
    {
        [TestMethod]
        public void TestPostLinkShouldNotCrash()
        {
            QueryRunner.GetResults(new ParsedQuery("select top 10 Id as [Post Link] from Posts", null), Current.DB.Sites.First(), new User());
        }
    }
}
