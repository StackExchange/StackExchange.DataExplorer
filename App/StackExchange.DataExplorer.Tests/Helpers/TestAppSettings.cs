using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Tests.Util {
    [TestClass]
    public class TestAppSettings : BaseTest
    {
        [TestMethod]
        public void TestBoolSetting() {
            Current.DB.Execute("delete from AppSettings");
            AppSettings.Refresh();
            Assert.IsFalse(AppSettings.EnableWhiteList);
            Current.DB.Insert("AppSettings", new { Setting = "EnableWhiteList", Value = "true" });
            AppSettings.Refresh();
            Assert.IsTrue(AppSettings.EnableWhiteList);
        }
    }
}
