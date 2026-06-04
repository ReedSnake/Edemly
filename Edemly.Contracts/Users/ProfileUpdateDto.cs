namespace Edemly.Contracts.Users
{
    public class ProfileUpdateDto
    {
        public int UserId { get; set; }
        public string PfpUrl { get; set; } = string.Empty;
    }
}