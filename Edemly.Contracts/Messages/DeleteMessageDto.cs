using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Messages
{
    public class DeleteMessageDto
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int ChatId { get; set; }
    }
}