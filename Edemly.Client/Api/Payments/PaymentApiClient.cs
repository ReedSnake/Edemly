using System.Globalization;
using System.Net.Http;
using Edemly.Client.Api.Core;
using Edemly.Contracts.Payments;

namespace Edemly.Client.Api.Payments;

public sealed class PaymentApiClient : ApiClientBase, IPaymentApiClient
{
    public PaymentApiClient(ApiClientContext context) : base(context)
    {
    }

    public async Task<(bool Success, string? Html, string? Error)> InitiatePaymentAsync(decimal amount)
    {
        try
        {
            var amountText = amount.ToString(CultureInfo.InvariantCulture);
            var url = UrlHelper.BuildRelativeUrl($"api/payment/initiate?amount={amountText}");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[API] InitiatePaymentAsync failed: {content}");
                return (false, null, content);
            }

            return (true, content, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] InitiatePaymentAsync exception: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<List<PaymentDto>> GetPaymentHistoryAsync()
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl("api/payment/history");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetPaymentHistoryAsync failed: {responseContent}");
                return new List<PaymentDto>();
            }

            var wrapped = Deserialize<PaymentHistoryResponseDto>(responseContent);
            if (wrapped?.Payments != null)
                return wrapped.Payments;

            return Deserialize<List<PaymentDto>>(responseContent) ?? new List<PaymentDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] GetPaymentHistoryAsync exception: {ex.Message}");
            return new List<PaymentDto>();
        }
    }

    public async Task<(bool Success, bool IsPaid, string? Error)> CheckPaymentStatusAsync(string orderId)
    {
        try
        {
            var escapedOrderId = Uri.EscapeDataString(orderId);
            var url = UrlHelper.BuildRelativeUrl($"api/payment/status/{escapedOrderId}");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[API] CheckPaymentStatusAsync failed: {content}");
                return (false, false, content);
            }

            var status = Deserialize<PaymentStatusResponseDto>(content);
            return (true, status?.IsPaid ?? false, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] CheckPaymentStatusAsync exception: {ex.Message}");
            return (false, false, ex.Message);
        }
    }
}
