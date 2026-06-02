namespace uchat.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class LoginWithCodeRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class SessionLoginRequestDto
    {
        public string SessionToken { get; set; } = string.Empty;
    }
}