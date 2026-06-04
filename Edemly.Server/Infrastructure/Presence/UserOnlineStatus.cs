namespace Edemly.Server.Models
{
    public class UserOnlineStatus
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public string? ConnectionId { get; set; }
    }
}