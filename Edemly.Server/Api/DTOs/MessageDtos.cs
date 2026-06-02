using System.ComponentModel.DataAnnotations;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.DTOs
{
    public class MessageDtos
    {
        public class MessageGetDto
        {
            public int Id { get; set; }
            public int ChatId { get; set; }
            public int SenderId { get; set; }
            public string Text { get; set; } = string.Empty;
            public DateTime SentAt { get; set; }
            public MessageType Type { get; set; }
            public string? ContentUrl { get; set; }
            public string? FileName { get; set; }
        }

        public class MessageCreateDto
        {
            [Required]
            public int ChatId { get; set; }

            [Required]
            public string Text { get; set; } = string.Empty;

            [Required]
            public MessageType Type { get; set; }

            public string? ContentUrl { get; set; }
            
            public string? FileName { get; set; }
        }

        public class MessageUpdateDto
        {
            [Required]
            public int Id { get; set; }
            [Required]
            public int ChatId { get; set; }

            [Required]
            public string? Text { get; set; }

            public MessageType? Type { get; set; }

            public string? ContentUrl { get; set; }
        }

        public class MessageDeleteDto
        {
            [Required]
            public int Id { get; set; }
            [Required]
            public int ChatId { get; set; }
        }
    }
}
