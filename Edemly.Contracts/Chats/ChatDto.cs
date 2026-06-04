namespace Edemly.Contracts.Chats
{
    public class ChatDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public int Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public string? LastMessageText { get; set; }
        public int? LastMessageSenderId { get; set; }
    }
}