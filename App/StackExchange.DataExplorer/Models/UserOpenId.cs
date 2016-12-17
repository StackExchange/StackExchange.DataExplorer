namespace StackExchange.DataExplorer.Models
{
    public class UserOpenId
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string OpenIdClaim { get; set; }
        public bool IsSecure { get; set; }
    }
}