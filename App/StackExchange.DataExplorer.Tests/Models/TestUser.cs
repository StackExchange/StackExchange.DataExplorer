using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Transactions;
using StackExchange.DataExplorer.Models;
using System.Data.Common;

namespace StackExchange.DataExplorer.Tests.Models {

    [TestClass]
    public class TestUser : BaseTest {

        [TestMethod]
        public void TestUserCreationSetsCreationDate() {
            var u = User.CreateUser("Fred", "a@a.com", "xyz");
            Assert.IsNotNull(u.CreationDate);
        }

        [TestMethod]
        public void TestBasicUserCreation() {

            User.CreateUser("Fred", "a@a.com", "xyz");

            var u2 = Current.DB.Users.First(u => u.Login == "Fred");
            Assert.AreEqual("Fred", u2.Login);

            var o = Current.DB.UserOpenIds.First(oid => oid.OpenIdClaim == "xyz");
            Assert.AreEqual("xyz", o.OpenIdClaim);
        }

        [TestMethod]
        public void TestUserNameExtrapolation() {
            var u1 = User.CreateUser("", "a@a.com", "xyz");
            var u2 = User.CreateUser(null, "a@ab.com", "xyz1");

            Assert.AreEqual("a", u1.Login);
            Assert.AreEqual("a1", u2.Login); 
        }

        [TestMethod]
        public void TestNoName() {

            Current.DB.Users.DeleteAllOnSubmit(Current.DB.Users.Where(u => u.Login.StartsWith("jon.doe")));
            Current.DB.SubmitChanges();

            var u1 = User.CreateUser("", null, "xyz");
            var u2 = User.CreateUser(null, "", "xyz1");

            Assert.AreEqual("jon.doe", u1.Login);
            Assert.AreEqual("jon.doe1", u2.Login);
        }

        [TestMethod]
        public void TestNoSpaces() {

            Current.DB.Users.DeleteAllOnSubmit(Current.DB.Users.Where(u => u.Login.StartsWith("jon.doe")));
            Current.DB.SubmitChanges();

            var u1 = User.CreateUser("jon   doe", null, "xyz");
            Assert.AreEqual("jon.doe", u1.Login);
        }

        [TestMethod]
        public void TestWeirdChars() {
            var u1 = User.CreateUser("jon&*doe", null, "xyz");
            Assert.AreEqual(u1.Login, "jondoe");
        } 
    }
}
