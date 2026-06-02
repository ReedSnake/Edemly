using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Chats
{
    public class CreateChatDto
    {
        [Required(ErrorMessage = "Chat name is required")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Chat name must be between 1 and 50 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public string? IconUrl { get; set; }

        [Required(ErrorMessage = "Chat type is required")]
        public int Type { get; set; }
    }
}
