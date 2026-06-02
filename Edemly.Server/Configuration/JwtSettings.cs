namespace Edemly.Server.Configuration
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int ExpiresInMinutes { get; set; } = 15;
        public int RefreshTokenExpiresInDays { get; set; } = 7;
    }
}