namespace Edemly.Contracts.ChatMembers
{
    public class ChatMemberDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ChatId { get; set; }
        public int Role { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}