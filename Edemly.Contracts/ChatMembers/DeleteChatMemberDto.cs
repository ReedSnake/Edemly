using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.ChatMembers
{
    public class DeleteChatMemberDto
    {
        [Required]
        public int Id { get; set; }
    }
}
