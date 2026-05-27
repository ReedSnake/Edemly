using System.ComponentModel.DataAnnotations;

namespace uchat_server.Api.DTOs
{
    public class ChatDtos
    {
        public class ChatCreateDto
        {
            [Required(ErrorMessage = "Chat name is required")]
            [StringLength(50, MinimumLength = 1, ErrorMessage = "Chat name must be between 1 and 50 characters")]
            public string Name { get; set; } = string.Empty;

            [StringLength(255)]
            public string? Description { get; set; }

            public string? IconUrl { get; set; }

            [Required(ErrorMessage = "Chat type is required")]
            public int Type { get; set; } // 0 = Private, 1 = Group, 2 = Channel
        }

        public class ChatUpdateDto
        {
            [Required]
            public int Id { get; set; }

            [StringLength(50, MinimumLength = 1, ErrorMessage = "Chat name must be between 1 and 50 characters")]
            public string? Name { get; set; }

            [StringLength(255)]
            public string? Description { get; set; }

            public string? IconUrl { get; set; }

            public int? Type { get; set; }
        }

        public class ChatDeleteDto
        {
            [Required(ErrorMessage = "Chat Id is required")]
            public int Id { get; set; }
        }

        public class ChatGetDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? IconUrl { get; set; }
            public int Type { get; set; } // 0 = Private, 1 = Group, 2 = Channel
            public DateTime CreatedAt { get; set; }
            public DateTime? LastMessageTime { get; set; }

            // ✅ ДОДАЙТЕ ЦІ ПОЛЯ
            public string? LastMessageText { get; set; }
            public int? LastMessageSenderId { get; set; }
        }
    }
}
