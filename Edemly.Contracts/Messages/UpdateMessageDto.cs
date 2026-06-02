using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Messages
{
    public class UpdateMessageDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int ChatId { get; set; }

        [Required]
        public string? Text { get; set; }

        public int? Type { get; set; }

        public string? ContentUrl { get; set; }

        public string? FileName { get; set; }
    }
}
