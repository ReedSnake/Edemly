using System;
using System.Collections.Generic;
using System.Text;

namespace Edemly.Contracts.Remindings
{
    public class RemindingDto
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

}
