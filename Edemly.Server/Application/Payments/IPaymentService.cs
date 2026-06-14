using Edemly.Contracts.Payments;
using Edemly.Server.Application.Common;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Application.Payments
{
    public interface IPaymentService
    {
        Task<ServiceResult> CreateAsync(int currentUserId, CreatePaymentDto request);

        Task<ServiceResult<PaymentDto>> GetByIdAsync(int paymentId);

        Task<ServiceResult<List<PaymentDto>>> GetByUserAsync(int targetUserId);

        Task<ServiceResult> UpdatePaymentStatusAsync(string transactionId, PaymentStatus newStatus);

        Task<ServiceResult> MarkPaidAndUpgradeUserAsync(string transactionId, int durationDays = 30);

        Task<ServiceResult> UpgradeUserToPremiumAsync(int targetUserId, int durationDays = 30);
    }
}
