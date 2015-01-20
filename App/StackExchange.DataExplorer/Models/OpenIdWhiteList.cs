using System;

namespace StackExchange.DataExplorer.Models
{
    public class OpenIdWhiteList
    {
        public int Id { get; set; }
        public string OpenId { get; set; }
        public bool Approved { get; set; }
        public string IpAddress { get; set; }
        public DateTime? CreationDate { get; set; }
    }
}