using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    [Table("reminding")]
    public class Reminding
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Column("last_time")]
        public DateTime LastTime { get; set; }

        [Column("type")]
        public int Type { get; set; } = 1;

        [Required]
        [Column("should_notify")]
        public bool ShouldNotify { get; set; }

        [Required]
        [Column("show_time")]
        public bool ShowTime { get; set; }

        [Column("is_completed")]
        public bool IsCompleted { get; set; } = false;

        // Navigation property
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}
