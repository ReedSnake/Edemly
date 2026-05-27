using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace uchat_server.Data.Entities
{
    [Table("message")]
    public class Message
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("chat_id")]
        public int ChatId { get; set; }

        [Required]
        [Column("sender_id")]
        public int SenderId { get; set; }

        [Required]
        [Column("text", TypeName = "text")]
        public string Text { get; set; } = string.Empty;

        [Required]
        [Column("sent_at")]
        public DateTime SentAt { get; set; }

        [Required]
        [Column("type")]
        public MessageType Type { get; set; }

        [MaxLength(255)]
        [Column("content_url")]
        public string? ContentUrl { get; set; }

        [MaxLength(500)]
        [Column("file_name")]
        public string? FileName { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ChatId))]
        public Chat Chat { get; set; } = null!;

        [ForeignKey(nameof(SenderId))]
        public User Sender { get; set; } = null!;
    }
}