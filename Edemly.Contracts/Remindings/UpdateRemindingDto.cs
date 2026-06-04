using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Remindings
{
    public class UpdateRemindingDto
    {
        [Required]
        public int Id { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        public string? Content { get; set; }

        public DateTime? LastTime { get; set; }

        public int? Type { get; set; }

        public bool? ShouldNotify { get; set; }

        public bool? ShowTime { get; set; }

        public bool? IsCompleted { get; set; }
    }
}