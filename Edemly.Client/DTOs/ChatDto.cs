#nullable disable
using System;

namespace Edemly.Client.DTOs
{
    public class ChatDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public int Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageTime { get; set; }
    }

    public class GroupChatCreatedDto
    {
        public int ChatId { get; set; }
        public string? ChatName { get; set; }
        public int ChatType { get; set; }
        public int CreatorId { get; set; }
    }
}