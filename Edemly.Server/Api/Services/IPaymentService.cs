using Edemly.Server.Api.DTOs;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public interface IPaymentService
    {
        Task<(bool Success, string? Error)> Create(int userId, PaymentDtos.PaymentCreateDto model);
        Task<(bool Success, string? Error, PaymentDtos.PaymentGetDto? Payment)> GetById(int id);
        Task<(bool Success, string? Error, List<PaymentDtos.PaymentGetDto> Payments)> GetByUser(int userId);
        Task<(bool Success, string? Error)> UpdatePaymentStatus(string transactionId, PaymentStatus newStatus);
        Task<(bool Success, string? Error)> UpgradeUserToPremium(int userId, int durationDays = 30);
    }
}
