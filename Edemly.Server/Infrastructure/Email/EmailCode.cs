using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Services
{
    /// <summary>
    /// Сутність для зберігання verification кодів для email
    /// </summary>
    [Table("email_codes")]
    public class EmailCode
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("expiration_time")]
        public DateTime ExpirationTime { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}