using Edemly.Contracts.Payments;
using Edemly.Server.Data.Entities;
using System.Linq.Expressions;

namespace Edemly.Server.Api.Services
{
    public static class PaymentMappings
    {
        public static readonly Expression<Func<Payment, PaymentDto>> Projection = payment => new PaymentDto
        {
            Id = payment.Id,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Status = payment.Status.ToString(),
            Date = payment.Date,
            UpdatedAt = payment.UpdatedAt,
            TransactionId = payment.TransactionId
        };

        public static PaymentDto ToDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                Date = payment.Date,
                UpdatedAt = payment.UpdatedAt,
                TransactionId = payment.TransactionId
            };
        }
    }
}