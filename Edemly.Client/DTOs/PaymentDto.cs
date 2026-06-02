#nullable disable

using System;

namespace Edemly.Client.DTOs
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string TransactionId { get; set; }
    }
}
