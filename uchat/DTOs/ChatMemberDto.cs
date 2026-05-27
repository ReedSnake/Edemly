#nullable disable
using System;

namespace uchat.DTOs
{
    public class ChatMemberDto
    {
        public int UserId { get; set; }
        public int ChatId { get; set; }
        public int Role { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}