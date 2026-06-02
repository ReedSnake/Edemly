using Edemly.Server.Data.Entities;
using Edemly.Contracts.Payments;
namespace Edemly.Server.Api.Services
{
    public interface IPaymentService
    {
        Task<(bool Success, string? Error)> Create(int userId, CreatePaymentDto model);
        Task<(bool Success, string? Error, PaymentDto Payment)> GetById(int id);
        Task<(bool Success, string? Error, List<PaymentDto> Payments)> GetByUser(int userId);
        Task<(bool Success, string? Error)> UpdatePaymentStatus(string transactionId, PaymentStatus newStatus);
        Task<(bool Success, string? Error)> UpgradeUserToPremium(int userId, int durationDays = 30);
    }
}
