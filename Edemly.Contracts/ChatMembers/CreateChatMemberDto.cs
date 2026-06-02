using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.ChatMembers
{
    public class CreateChatMemberDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public int ChatId { get; set; }

        [Required]
        public int Role { get; set; }
    }
}
