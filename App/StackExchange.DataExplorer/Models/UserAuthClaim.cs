using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Models
{
    public class UserAuthClaim
    {
        public enum ClaimType
        {
            OpenID = 1,
            Google = 2
        }

        public class Identifier
        {
            public readonly ClaimType Type;
            public readonly string Value;

            public Identifier(String value, ClaimType type)
            {
                Value = value;
                Type = type;
            }
        }

        public int Id { get; set; }
        public int UserId { get; set; }
        public string ClaimIdentifier { get; set; }
        public bool IsSecure { get; set; }
        public ClaimType IdentifierType { get; set; }
        public string Display { get; set; }
    }
}