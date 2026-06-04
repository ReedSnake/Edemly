namespace Edemly.Contracts.Users
{
    public class UserInfoDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PfpUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiration { get; set; }
    }
}