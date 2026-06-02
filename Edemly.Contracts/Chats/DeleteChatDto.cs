using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Chats
{
    public class DeleteChatDto
    {
        [Required(ErrorMessage = "Chat Id is required")]
        public int Id { get; set; }
    }
}
