using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Edemly.Server.Configuration;

namespace Edemly.Server.Data.Entities
{
    [Table("session_info")]
    public class Session
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("session_token")]
        public string SessionToken { get; set; } = string.Empty;

        [Required]
        [Column("expiration_time")]
        public DateTime ExpirationTime { get; set; }

        // Navigation property
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}
