using Edemly.Contracts.Payments;

namespace Edemly.Client.Api.Payments;

public interface IPaymentApiClient
{
    Task<(bool Success, string? Html, string? Error)> InitiatePaymentAsync(decimal amount);

    Task<List<PaymentDto>> GetPaymentHistoryAsync();

    Task<(bool Success, bool IsPaid, string? Error)> CheckPaymentStatusAsync(string orderId);
}