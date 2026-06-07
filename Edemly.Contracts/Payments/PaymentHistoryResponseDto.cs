namespace Edemly.Contracts.Payments;

public sealed class PaymentHistoryResponseDto
{
    public List<PaymentDto>? Payments { get; set; }
}