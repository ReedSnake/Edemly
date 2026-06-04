namespace Edemly.Contracts.Users
{
    public class UserStatusDto
    {
        public int UserId { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
    }
}