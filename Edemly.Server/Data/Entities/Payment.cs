using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edemly.Server.Data.Entities
{
    [Table("payment")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("amount", TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column("status")]
        public PaymentStatus Status { get; set; }

        [Required]
        [Column("date")]
        public DateTime Date { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [MaxLength(100)]
        [Column("transaction_id")]
        public string? TransactionId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}