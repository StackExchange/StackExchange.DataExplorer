using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Transactions;
using StackExchange.DataExplorer.Models;
using System.Data.Common;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Tests.Models {

    [TestClass]
    public class TestUser : BaseTest {
        [TestMethod]
        public void TestNoName() {

            Current.DB.Execute("delete from Users where Login like 'jon.doe%'");

            var u1 = User.CreateUser("", null);
            var u2 = User.CreateUser(null, "");

            Assert.AreEqual("jon.doe", u1.Login);
            // This behaviour is probably not what we want
            Assert.AreEqual("jon.doe" + (u1.Id + 2), u2.Login);
        }

        [TestMethod]
        public void TestNoSpaces() {

            Current.DB.Execute("delete from Users where Login like 'jon.doe%'");

            var u1 = User.CreateUser("jon   doe", null);
            Assert.AreEqual("jon.doe", u1.Login);
        }

        [TestMethod]
        public void TestWeirdChars() {
            var u1 = User.CreateUser("jon&*doe", null);
            Assert.AreEqual(u1.Login, "jondoe");
        } 
    }
}
