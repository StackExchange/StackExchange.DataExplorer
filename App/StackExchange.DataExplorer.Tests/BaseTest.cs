using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StackExchange.DataExplorer.Tests {


    [TestClass]
    public class BaseTest {

        [TestInitialize]
        public void Setup() {

            if (Current.DB.Connection.State != System.Data.ConnectionState.Open) {
                Current.DB.Connection.Open();
            }
            Current.DB.Transaction = Current.DB.Connection.BeginTransaction();
        }

        [TestCleanup]
        public void Teardown() {
            Current.DB.Transaction.Rollback();
            Current.DB.Transaction.Dispose();
            Current.DB.Transaction = null;
            Current.DisposeDB();
        }
    }
}
