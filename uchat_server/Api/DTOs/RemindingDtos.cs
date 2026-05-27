using System.ComponentModel.DataAnnotations;
using uchat_server.Data.Entities;

namespace uchat_server.Api.DTOs
{
    public class RemindingDtos
    {
        public class RemindingGetDto
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime LastTime { get; set; }
            public int Type { get; set; } = 1;
            public bool ShouldNotify { get; set; }
            public bool ShowTime { get; set; }

            public bool IsCompleted { get; set; }
        }

        public class RemindingCreateDto
        {
            [Required]
            [MaxLength(255)]
            public string Name { get; set; } = string.Empty;

            public string Content { get; set; } = string.Empty;

            [Required]
            public DateTime LastTime { get; set; }

            [Required]
            public int Type { get; set; }

            public bool ShouldNotify { get; set; } = true;

            public bool ShowTime { get; set; } = false;
        }

        public class RemindingUpdateDto
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
}