using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.ChatMembers
{
    public class UpdateChatMemberDto
    {
        [Required]
        public int Id { get; set; }

        public int? Role { get; set; }
    }
}
