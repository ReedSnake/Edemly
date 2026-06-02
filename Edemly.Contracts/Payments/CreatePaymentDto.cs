using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Edemly.Contracts.Payments
{
    public class CreatePaymentDto
    {
        [Required]
        [Range(0.01, 10000, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        public string? TransactionId { get; set; }
    }
}
