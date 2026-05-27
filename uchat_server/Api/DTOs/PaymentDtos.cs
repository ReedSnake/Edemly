using System.ComponentModel.DataAnnotations;
using uchat_server.Data.Entities;

namespace uchat_server.Api.DTOs
{
    public class PaymentDtos
    {
        public class PaymentGetDto
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public decimal Amount { get; set; }
            public PaymentStatus Status { get; set; }
            public DateTime Date { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string? TransactionId { get; set; }
        }

        public class PaymentCreateDto
        {
            [Required]
            [Range(0.01, 10000, ErrorMessage = "Amount must be positive")]
            public decimal Amount { get; set; }

            [Required]
            public PaymentStatus Status { get; set; }

            [Required]
            public DateTime Date { get; set; }

            public string? TransactionId { get; set; }
        }
    }
}
