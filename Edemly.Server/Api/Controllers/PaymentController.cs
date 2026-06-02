using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using uchat_server.Api.Services;
using uchat_server.Data.Entities;
using uchat_server.Data;
using uchat_server.Services;
using uchat_server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace uchat_server.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ApiControllerBase
    {
        private readonly WayForPayService _wayForPayService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            WayForPayService wayForPayService,
            IPaymentService paymentService,
            ILogger<PaymentController> logger,
            IConfiguration configuration,
            ServerDbContext serverDb,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _wayForPayService = wayForPayService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet("initiate")]
        [Authorize]
        public async Task<IActionResult> InitiatePayment([FromQuery] decimal amount = 100.00m)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            if (amount < 0.01m || amount > 10000m)
            {
                return BadRequest(new { error = "Invalid amount. Must be between 0.01 and 10000 UAH" });
            }

            var result = await _wayForPayService.GeneratePaymentFormAsync(userId, amount);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Error });
            }

            return Content(result.FormHtml!, "text/html");
        }

        [HttpPost("return")]
        public async Task<IActionResult> ReturnFromPayment([FromForm] string orderReference, [FromForm] string? testSuccess)
        {
            if (string.IsNullOrEmpty(orderReference))
            {
                return BadRequest("Order reference is missing");
            }

            _logger.LogInformation("User returned from payment for order: {OrderReference}", orderReference);

            var isTestMode = Configuration.GetValue<bool>("WayForPay:TestMode", false);
            bool isPaid = false;

            if (isTestMode && !string.IsNullOrEmpty(testSuccess))
            {
                // Тестовий режим - беремо значення з форми
                isPaid = testSuccess.Equals("true", StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation("TEST MODE: Payment {OrderReference} result: {IsPaid}", orderReference, isPaid);
            }
            else
            {
                // Перевіряємо статус платежу через API WayForPay
                var checkResult = await _wayForPayService.CheckPaymentStatusAsync(orderReference);

                if (!checkResult.Success)
                {
                    return StatusCode(500, GenerateResultPage(false, checkResult.Error ?? "Unknown error"));
                }

                isPaid = checkResult.IsPaid;
            }

            if (isPaid)
            {
                // Оновлюємо статус платежу в БД
                await _paymentService.UpdatePaymentStatus(orderReference, PaymentStatus.Paid);

                // Отримуємо UserId з orderReference (формат: "User_123_Order_...")
                var userId = ExtractUserIdFromOrderReference(orderReference);

                if (userId > 0)
                {
                    // Даємо користувачу Premium на 30 днів
                    var upgradeResult = await _paymentService.UpgradeUserToPremium(userId, 30);

                    if (upgradeResult.Success)
                    {
                        _logger.LogInformation("User {UserId} successfully upgraded to Premium", userId);
                        return Content(GenerateResultPage(true, "Оплату успішно підтверджено! Ваш акаунт тепер Premium."), "text/html");
                    }
                }

                return Content(GenerateResultPage(false, "Помилка при оновленні статусу користувача"), "text/html");
            }
            else
            {
                // Оновлюємо статус на Failed
                await _paymentService.UpdatePaymentStatus(orderReference, PaymentStatus.Failed);

                return Content(GenerateResultPage(false, "Оплату не підтверджено. Спробуйте ще раз."), "text/html");
            }
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { error = "User not authenticated" });
            }

            var result = await _paymentService.GetByUser(userId);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new { payments = result.Payments });
        }

        [HttpGet("status/{orderId}")]
        [Authorize]
        public async Task<IActionResult> CheckPaymentStatus(string orderId)
        {
            var result = await _wayForPayService.CheckPaymentStatusAsync(orderId);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Error });
            }

            return Ok(new
            {
                orderId,
                isPaid = result.IsPaid,
                status = result.IsPaid ? "Approved" : "Pending/Failed"
            });
        }

        private int ExtractUserIdFromOrderReference(string orderReference)
        {
            try
            {
                var parts = orderReference.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int userId))
                {
                    return userId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract UserId from orderReference: {OrderReference}", orderReference);
            }

            return 0;
        }

        private string GenerateResultPage(bool success, string message)
        {
            var color = success ? "#4CAF50" : "#f44336";
            var icon = success ? "✓" : "✗";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{(success ? "Успіх" : "Помилка")}</title>
    <style>
        body {{ font-family: Arial, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background-color: #f0f0f0; }}
        .container {{ text-align: center; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .icon {{ font-size: 72px; color: {color}; margin-bottom: 20px; }}
        h1 {{ color: {color}; margin: 0; }}
        p {{ color: #666; margin-top: 10px; font-size: 18px; }}
        .button {{ display: inline-block; margin-top: 20px; padding: 12px 30px; background-color: {color}; color: white; text-decoration: none; border-radius: 5px; font-weight: bold; }}
        .button:hover {{ opacity: 0.9; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>{icon}</div>
        <h1>{(success ? "Успішно!" : "Помилка")}</h1>
        <p>{message}</p>
        <a href='#' class='button' onclick='window.close(); return false;'>Закрити</a>
    </div>
</body>
</html>";
        }
    }
}