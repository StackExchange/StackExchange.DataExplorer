using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Tests.Models
{
    [TestClass]
    public class TestUser : BaseTest
    {
        [TestMethod]
        public void TestUserCreationSetsCreationDate()
        {
            var u = User.CreateUser("Fred", "a@a.com", "xyz");
            Assert.IsNotNull(u.CreationDate);
        }

        [TestMethod]
        public void TestBasicUserCreation()
        {
            User.CreateUser("Fred", "a@a.com", "xyz");

            var u2 = Current.DB.Query<User>("select * from Users where Login = @Login", new { Login = "Fred" }).First();
            Assert.AreEqual("Fred", u2.Login);

            var o = Current.DB.Query<UserOpenId>("select * from UserOpenIds where OpenIdClaim = @claim", new { claim = "xyz" }).FirstOrDefault();
            Assert.AreEqual("xyz", o.OpenIdClaim);
        }

        [TestMethod]
        public void TestNoName()
        {
            Current.DB.Execute("delete from Users where Login like 'jon.doe%'");

            var u1 = User.CreateUser("", null, "xyz");
            var u2 = User.CreateUser(null, "", "xyz1");

            Assert.AreEqual("jon.doe", u1.Login);
            // This behaviour is probably not what we want
            Assert.AreEqual("jon.doe" + (u1.Id + 2), u2.Login);
        }

        [TestMethod]
        public void TestNoSpaces()
        {
            Current.DB.Execute("delete from Users where Login like 'jon.doe%'");

            var u1 = User.CreateUser("jon   doe", null, "xyz");
            Assert.AreEqual("jon.doe", u1.Login);
        }

        [TestMethod]
        public void TestWeirdChars()
        {
            var u1 = User.CreateUser("jon&*doe", null, "xyz");
            Assert.AreEqual(u1.Login, "jondoe");
        }
    }
}
