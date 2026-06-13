using Edemly.Contracts.Payments;
using Edemly.Server.Application.Payments;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Hosting;
using System.Security.Cryptography;
using System.Text;

namespace Edemly.Server.Infrastructure.Payments
{
    public class WayForPayService
    {
        private readonly string _merchantAccount;
        private readonly string _secretKey;
        private readonly bool _testMode;
        private readonly HttpClient _httpClient;
        private readonly ILogger<WayForPayService> _logger;
        private readonly IPaymentService _paymentService;

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        private readonly IPublicUrlProvider _publicUrlProvider;

        public WayForPayService(
            IConfiguration config,
            HttpClient httpClient,
            ILogger<WayForPayService> logger,
            IPaymentService paymentService,
            IPublicUrlProvider publicUrlProvider,
            IHttpContextAccessor httpContextAccessor) // Інжектимо аксесор
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
            _paymentService = paymentService;
            _publicUrlProvider = publicUrlProvider;
            _httpContextAccessor = httpContextAccessor;

            _merchantAccount = config["WayForPay:MerchantAccount"]
                ?? throw new InvalidOperationException("WayForPay:MerchantAccount not configured");
            _secretKey = config["WayForPay:SecretKey"]
                ?? throw new InvalidOperationException("WayForPay:SecretKey not configured");

            _testMode = config.GetValue<bool>("WayForPay:TestMode", false);
        }

        private (string DomainName, string ReturnUrl) ResolveUrls()
        {
            var domainFromConfig = _config["WayForPay:DomainName"];
            if (!string.IsNullOrWhiteSpace(domainFromConfig))
            {
                var returnUrl = _config["WayForPay:ReturnUrl"] ?? $"{domainFromConfig.TrimEnd('/')}/api/payment/return";
                return (domainFromConfig, returnUrl);
            }

            var publicUrl = _publicUrlProvider?.GetPublicBaseUrl();
            if (!string.IsNullOrWhiteSpace(publicUrl) && Uri.TryCreate(publicUrl, UriKind.Absolute, out var u))
            {
                string domain = $"{u.Scheme}://{u.Host}{(u.IsDefaultPort ? "" : ":" + u.Port)}";
                return (domain, $"{domain}/api/payment/return");
            }

            var request = _httpContextAccessor.HttpContext?.Request;
            if (request != null)
            {
                string dynamicDomain = $"{request.Scheme}://{request.Host}";
                return (dynamicDomain, $"{dynamicDomain}/api/payment/return");
            }

            throw new InvalidOperationException("Cannot resolve DomainName. No HTTP context, config, or public URL available.");
        }

        public async Task<(bool Success, string? Error, string? FormHtml)> GeneratePaymentFormAsync(
            int userId,
            decimal amount,
            string productName = "Premium Subscription")
        {
            try
            {
                var orderId = $"User_{userId}_Order_{Guid.NewGuid():N}";
                var createResult = await CreatePaymentAsync(userId, amount, orderId);

                if (!createResult.Success)
                    return (false, createResult.Error, null);

                var (domainName, returnUrl) = ResolveUrls();

                if (_testMode)
                {
                    var testForm = GenerateTestPaymentForm(orderId, amount, userId, returnUrl);
                    _logger.LogInformation("TEST MODE: Payment form generated for User {UserId}, OrderId: {OrderId}", userId, orderId);
                    return (true, null, testForm);
                }

                var orderDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var amountStr = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var currency = "UAH";
                var productCount = "1";

                var dataToSign = string.Join(";", new[]
                {
                    _merchantAccount, domainName, orderId, orderDate, amountStr, currency,
                    productName, productCount, amountStr
                });

                var signature = GenerateSignature(dataToSign, _secretKey);

                var htmlForm = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Перенаправлення на оплату</title>
                    </head>
                <body>
                    <form id='paymentForm' method='POST' action='https://secure.wayforpay.com/pay' accept-charset='utf-8'>
                        <input type='hidden' name='merchantAccount' value='{_merchantAccount}'>
                        <input type='hidden' name='merchantAuthType' value='SimpleSignature'>
                        <input type='hidden' name='merchantDomainName' value='{domainName}'>
                        <input type='hidden' name='orderReference' value='{orderId}'>
                        <input type='hidden' name='orderDate' value='{orderDate}'>
                        <input type='hidden' name='amount' value='{amountStr}'>
                        <input type='hidden' name='currency' value='{currency}'>
                        <input type='hidden' name='productName[]' value='{productName}'>
                        <input type='hidden' name='productCount[]' value='{productCount}'>
                        <input type='hidden' name='productPrice[]' value='{amountStr}'>
                        <input type='hidden' name='merchantSignature' value='{signature}'>
                        <input type='hidden' name='returnUrl' value='{returnUrl}'>
                    </form>
                    <script>
                        document.getElementById('paymentForm').submit();
                    </script>
                </body>
                </html>";

                _logger.LogInformation("Payment form generated for User {UserId}, OrderId: {OrderId}, Amount: {Amount}", userId, orderId, amount);
                return (true, null, htmlForm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate payment form for user {UserId}", userId);
                return (false, "Failed to generate payment form", null);
            }
        }

        private async Task<(bool Success, string? Error)> CreatePaymentAsync(int userId, decimal amount, string orderId)
        {
            var result = await _paymentService.CreateAsync(userId, new CreatePaymentDto
            {
                Amount = amount,
                Status = PaymentStatus.Pending.ToString(),
                Date = DateTime.UtcNow,
                TransactionId = orderId
            });

            return (result.Success, result.Success ? null : result.Message);
        }

        private string GenerateTestPaymentForm(string orderId, decimal amount, int userId, string returnUrl)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <title>ТЕСТОВИЙ РЕЖИМ - Оплата</title>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        background: #f3faf8;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 100vh;
                        margin: 0;
                        color: #123;
                    }}
                    .card {{
                        width: min(420px, calc(100vw - 32px));
                        background: white;
                        border-radius: 16px;
                        box-shadow: 0 12px 30px rgba(0,0,0,.12);
                        padding: 28px;
                        text-align: center;
                    }}
                    h1 {{
                        margin: 0 0 10px;
                        font-size: 24px;
                    }}
                    .muted {{
                        color: #667;
                        font-size: 14px;
                        margin-bottom: 20px;
                    }}
                    .amount {{
                        font-size: 34px;
                        font-weight: 700;
                        margin: 18px 0;
                    }}
                    .order {{
                        word-break: break-all;
                        background: #eef6f4;
                        border-radius: 8px;
                        padding: 10px;
                        font-size: 12px;
                        margin-bottom: 20px;
                    }}
                    button {{
                        width: 100%;
                        border: 0;
                        border-radius: 10px;
                        padding: 13px 16px;
                        margin-top: 10px;
                        font-size: 16px;
                        font-weight: 700;
                        cursor: pointer;
                    }}
                    .success {{
                        background: #0b8f72;
                        color: white;
                    }}
                    .fail {{
                        background: #e8ecef;
                        color: #27313a;
                    }}
                </style>
                </head>
            <body>
                <div class='card'>
                    <h1>Тестова оплата Premium</h1>
                    <div class='muted'>Це тестовий режим WayForPay. Реальні кошти не списуються.</div>
                    <div class='amount'>{amount:0.00} UAH</div>
                    <div class='order'>Order: {orderId}</div>
                    <button class='success' onclick='simulatePayment(true)'>Підтвердити оплату</button>
                    <button class='fail' onclick='simulatePayment(false)'>Відхилити оплату</button>
                </div>
                <script>
                    function simulatePayment(success) {{
                        const form = document.createElement('form');
                        form.method = 'POST';
                        form.action = '{returnUrl}'; // Динамічний URL

                        const input = document.createElement('input');
                        input.type = 'hidden';
                        input.name = 'orderReference';
                        input.value = '{orderId}';

                        const statusInput = document.createElement('input');
                        statusInput.type = 'hidden';
                        statusInput.name = 'testSuccess';
                        statusInput.value = success.toString();

                        form.appendChild(input);
                        form.appendChild(statusInput);
                        document.body.appendChild(form);
                        form.submit();
                    }}
                </script>
            </body>
            </html>";
        }

        public async Task<(bool Success, string? Error, bool IsPaid)> CheckPaymentStatusAsync(string orderId)
        {
            return (true, null, true); // Заглушка для прикладу
        }

        private string GenerateSignature(string data, string key)
        {
            using (var hmac = new HMACMD5(Encoding.UTF8.GetBytes(key)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}
