using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Edemly.Contracts.Payments;

namespace Edemly.Client.Services.Api
{
    public partial class ApiService
    {
        public async Task<(bool Success, string? Html, string? Error)> InitiatePaymentAsync(decimal amount)
        {
            try
            {
                var rel = $"api/payment/initiate?amount={amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);
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
                var rel = "api/payment/history";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[API] GetPaymentHistoryAsync failed: {responseContent}");
                    return new List<PaymentDto>();
                }

                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("payments", out var paymentsEl))
                {
                    var paymentsJson = paymentsEl.GetRawText();
                    var list = TryDeserialize<List<PaymentDto>>(paymentsJson);
                    return list ?? new List<PaymentDto>();
                }

                var maybeList = TryDeserialize<List<PaymentDto>>(responseContent);
                return maybeList ?? new List<PaymentDto>();
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
                var rel = $"api/payment/status/{Uri.EscapeDataString(orderId)}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[API] CheckPaymentStatusAsync failed: {content}");
                    return (false, false, content);
                }

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("isPaid", out var isPaidEl) && (isPaidEl.ValueKind == JsonValueKind.True || isPaidEl.ValueKind == JsonValueKind.False))
                {
                    bool isPaid = isPaidEl.GetBoolean();
                    return (true, isPaid, null);
                }

                var wrapper = TryDeserialize<Dictionary<string, object>>(content);
                if (wrapper != null && wrapper.TryGetValue("isPaid", out var o) && o is bool b)
                {
                    return (true, b, null);
                }

                return (true, false, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] CheckPaymentStatusAsync exception: {ex.Message}");
                return (false, false, ex.Message);
            }
        }
    }
}
