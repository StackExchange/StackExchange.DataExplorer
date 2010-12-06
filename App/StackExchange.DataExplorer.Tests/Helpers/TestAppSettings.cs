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
            Current.DB.AppSettings.DeleteAllOnSubmit(Current.DB.AppSettings);
            Current.DB.SubmitChanges();

            AppSettings.Refresh();

            Assert.IsFalse(AppSettings.EnableWhiteList);

            Current.DB.AppSettings.InsertOnSubmit(new AppSetting { Setting = "EnableWhiteList", Value = "true" });
            Current.DB.SubmitChanges();
            
            AppSettings.Refresh();

            Assert.IsTrue(AppSettings.EnableWhiteList);
        }
    }
}
