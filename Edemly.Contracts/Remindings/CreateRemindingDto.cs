using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edemly.Contracts.Remindings
{
    public class CreateRemindingDto
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

        public bool IsCompleted { get; set; } = false;

    }
}
