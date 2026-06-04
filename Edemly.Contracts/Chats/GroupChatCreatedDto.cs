namespace Edemly.Contracts.Chats
{
    public class GroupChatCreatedDto
    {
        public int ChatId { get; set; }
        public string? ChatName { get; set; }
        public int ChatType { get; set; }
        public int CreatorId { get; set; }
    }
}