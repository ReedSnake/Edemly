using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    [Table("user")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("login_info_id")]
        public int LoginInfoId { get; set; }

        [MaxLength(50)]
        [Column("username")]
        public string? Username { get; set; }

        [MaxLength(255)]
        [Column("pfp_url")]
        public string? PfpUrl { get; set; }

        [Column("last_online")]
        public DateTime? LastOnline { get; set; }

        // User details fields (перенесені з UserDetails)
        [MaxLength(100)]
        [Column("first_name")]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        [Column("last_name")]
        public string? LastName { get; set; }

        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        [MaxLength(25)]
        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [MaxLength(255)]
        [Column("location")]
        public string? Location { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("subscription_status")]
        public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Free;

        [Column("subscription_expiration")]
        public DateTime? SubscriptionExpiration { get; set; }

        // Navigation properties
        [ForeignKey(nameof(LoginInfoId))]
        public LoginInfo LoginInfo { get; set; } = null!;

        public Session? Session { get; set; }

        public ICollection<Note> NotesAboutUser { get; set; } = new List<Note>();
        public ICollection<Note> NotesCreatedByUser { get; set; } = new List<Note>();
        public ICollection<Reminding> Remindings { get; set; } = new List<Reminding>();
        public ICollection<ChatMember> ChatMembers { get; set; } = new List<ChatMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
