using Edemly.Server.Data.Entities;
using Edemly.Contracts.Payments;
namespace Edemly.Server.Api.Services
{
    public interface IPaymentService
    {
        Task<ServiceMessageResult> Create(int userId, CreatePaymentDto model);
        Task<ServiceDataResult<PaymentDto>> GetById(int id);
        Task<ServiceDataResult<List<PaymentDto>>> GetByUser(int userId);
        Task<ServiceMessageResult> UpdatePaymentStatus(string transactionId, PaymentStatus newStatus);
        Task<ServiceMessageResult> UpgradeUserToPremium(int userId, int durationDays = 30);
    }
}
