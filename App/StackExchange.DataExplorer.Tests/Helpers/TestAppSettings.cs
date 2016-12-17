using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StackExchange.DataExplorer.Tests.Helpers
{
    [TestClass]
    public class TestAppSettings : BaseTest
    {
        [TestMethod]
        public void TestBoolSetting()
        {
            Current.DB.Execute("delete from AppSettings");
            AppSettings.Refresh();
            Assert.IsFalse(AppSettings.EnableWhiteList);
            Current.DB.AppSettings.Insert(new { Setting = "EnableWhiteList", Value = "true" });
            AppSettings.Refresh();
            Assert.IsTrue(AppSettings.EnableWhiteList);
        }

        [TestMethod]
        public void TestObjectSetting()
        {
            Current.DB.Execute("DELETE FROM AppSettings");
            AppSettings.Refresh();
            Assert.IsNull(AppSettings.HelperTableOptions);
            Current.DB.AppSettings.Insert(new
            {
                Setting = "HelperTableOptions",
                Value = "{ \"PerSite\": \"true\", \"IncludePattern\": \".*Types$\" }"
            });
            AppSettings.Refresh();
            Assert.IsNotNull(AppSettings.HelperTableOptions);
            Assert.IsTrue(AppSettings.HelperTableOptions.PerSite);
            Assert.AreEqual(".*Types$", AppSettings.HelperTableOptions.IncludePattern);
        }
    }
}
