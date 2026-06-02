using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace uchat_server.Data.Entities
{
    [Table("login_info")]
    public class LoginInfo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;


        [Column("is_email_verified")]
        public bool IsEmailVerified { get; set; } = false;

        // Navigation property
        public User? User { get; set; }
    }
}