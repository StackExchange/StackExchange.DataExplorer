using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public class UserOpenId
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string OpenIdClaim { get; set; }
    }
}