using System.ComponentModel.DataAnnotations;
using uchat_server.Data.Entities;

namespace uchat_server.Api.DTOs
{
    public class ChatMemberDtos
    {
        public class ChatMemberGetDto
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public int ChatId { get; set; }
            public ChatMemberRole Role { get; set; }
            public DateTime JoinedAt { get; set; }
        }

        public class ChatMemberCreateDto
        {
            [Required]
            public int UserId { get; set; }

            [Required]
            public int ChatId { get; set; }

            [Required]
            public ChatMemberRole Role { get; set; }
        }

        public class ChatMemberUpdateDto
        {
            [Required]
            public int Id { get; set; }

            public ChatMemberRole? Role { get; set; }
        }

        public class ChatMemberDeleteDto
        {
            [Required]
            public int Id { get; set; }
        }
    }
}
