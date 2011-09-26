using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Tests.Util {
    [TestClass]
    public class TestQueryRunner {
        [TestMethod]
        public void TestPostLinkShouldNotCrash() {
            QueryRunner.GetSingleSiteResults(new ParsedQuery("select top 10 Id as [Post Link] from Posts", null), Current.DB.Sites.First() , new User());
        }
    }
}
