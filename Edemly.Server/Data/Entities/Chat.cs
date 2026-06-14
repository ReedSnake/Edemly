using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    [Table("chat")]
    public class Chat
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [MaxLength(255)]
        [Column("icon_url")]
        public string? IconUrl { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("type")]
        public ChatType Type { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("last_message_time")]
        public DateTime? LastMessageTime { get; set; }

        [Column("last_message_id")]
        public int? LastMessageId { get; set; }

        [Column("last_message_text")]
        public string? LastMessageText { get; set; }

        [Column("last_message_sender_id")]
        public int? LastMessageSenderId { get; set; }

        public ICollection<ChatMember> ChatMembers { get; set; } = new List<ChatMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
