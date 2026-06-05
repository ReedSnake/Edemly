namespace Edemly.Client.Application.Session
{
    public sealed class ClientUserSession
    {
        public int? UserId { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? PhotoUrl { get; set; }
        public string? AuthToken { get; set; }

        public void SetCurrentUser(int userId, string email, string userName, string? photoUrl = null, string? token = null)
        {
            UserId = userId;
            Email = email;
            UserName = userName;
            PhotoUrl = photoUrl;

            if (!string.IsNullOrEmpty(token))
            {
                AuthToken = token;
            }
        }

        public void Clear()
        {
            UserId = null;
            Email = null;
            UserName = null;
            PhotoUrl = null;
            AuthToken = null;
        }
    }
}
