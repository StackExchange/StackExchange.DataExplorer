using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using StackExchange.DataExplorer.Helpers;

namespace StackExchange.DataExplorer.Models {
    public partial class SavedQuery {

       public void UpdateQueryBodyComment() {
            Query.Name = Title;
            Query.Description = Description;
            Query.UpdateQueryBodyComment();
        }
    }
}
