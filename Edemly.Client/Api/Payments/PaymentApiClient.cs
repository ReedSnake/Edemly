using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Edemly.Client.Api.Core;

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

            using var doc = JsonDocument.Parse(responseContent);

            if (doc.RootElement.TryGetProperty("payments", out var paymentsElement))
            {
                var paymentsJson = paymentsElement.GetRawText();
                return Deserialize<List<PaymentDto>>(paymentsJson) ?? new List<PaymentDto>();
            }

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

            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("isPaid", out var isPaidElement) &&
                isPaidElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return (true, isPaidElement.GetBoolean(), null);
            }

            var wrapper = Deserialize<Dictionary<string, object>>(content);

            if (wrapper != null &&
                wrapper.TryGetValue("isPaid", out var value) &&
                value is bool isPaid)
            {
                return (true, isPaid, null);
            }

            return (true, false, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] CheckPaymentStatusAsync exception: {ex.Message}");
            return (false, false, ex.Message);
        }
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] JSON parse failed: {ex.Message}");
            return default;
        }
    }
}