namespace Edemly.Contracts.Users
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PfpUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
