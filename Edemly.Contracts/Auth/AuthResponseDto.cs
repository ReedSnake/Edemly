namespace Edemly.Contracts.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
