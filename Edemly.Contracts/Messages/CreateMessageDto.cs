using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Messages
{
    public class CreateMessageDto
    {
        [Required]
        public int ChatId { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;

        [Required]
        public int Type { get; set; }

        public string? ContentUrl { get; set; }

        public string? FileName { get; set; }
    }
}