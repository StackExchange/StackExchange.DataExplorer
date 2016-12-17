using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StackExchange.DataExplorer.Tests
{
    [TestClass]
    public class BaseTest
    {
        [TestInitialize]
        public void Setup()
        {
            Current.DB.BeginTransaction();
        }

        [TestCleanup]
        public void Teardown()
        {
            Current.DB.RollbackTransaction();
            Current.DisposeDB();
        }
    }
}
