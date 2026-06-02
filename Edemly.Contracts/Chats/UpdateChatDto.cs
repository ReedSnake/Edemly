using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Chats
{
    public class UpdateChatDto
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
}
