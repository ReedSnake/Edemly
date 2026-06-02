#nullable disable

namespace uchat.DTOs
{
    public class UserInfoDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; }
        public string PfpUrl { get; set; }
        public string Description { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Location { get; set; }
        public string SubscriptionStatus { get; set; }
        public DateTime? SubscriptionExpiration { get; set; }
    }
}