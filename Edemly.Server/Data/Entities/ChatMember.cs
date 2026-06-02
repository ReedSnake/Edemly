using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace uchat_server.Data.Entities
{
    [Table("chat_member")]
    public class ChatMember
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("chat_id")]
        public int ChatId { get; set; }

        [Required]
        [Column("role")]
        public ChatMemberRole Role { get; set; }

        [Required]
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(ChatId))]
        public Chat Chat { get; set; } = null!;
    }
}