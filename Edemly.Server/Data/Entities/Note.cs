using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    [Table("notes")]
    public class Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("creator_id")]
        public int CreatorId { get; set; }

        [Required]
        [Column("content", TypeName = "text")]
        public string Content { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(CreatorId))]
        public User Creator { get; set; } = null!;
    }
}