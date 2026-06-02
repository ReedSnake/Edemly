using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Contracts.Payments;
namespace Edemly.Server.Api.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public PaymentService(ServerDbContext serverDb, ILogger<PaymentService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }


        public async Task<(bool Success, string? Error)> Create(int userId, CreatePaymentDto model)
        {
            try
            {
                var user = await _ctx.Set<User>().FindAsync(userId);
                if (user == null)
                    return (false, "User not found");

                var payment = new Payment
                {
                    UserId = userId,
                    Amount = model.Amount,
                    Status = Enum.Parse<PaymentStatus>(model.Status),
                    Date = model.Date,
                    UpdatedAt = DateTime.UtcNow,
                    TransactionId = model.TransactionId
                };

                _ctx.Set<Payment>().Add(payment);
                await _ctx.SaveChangesAsync();

                _logger.LogInformation("Payment created for user {UserId}, Amount: {Amount}", userId, model.Amount);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment for user {UserId}", userId);
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, PaymentDto Payment)> GetById(int id)
        {
            try
            {
                var payment = await _ctx.Set<Payment>().FindAsync(id);
                if (payment == null)
                    return (false, "Payment not found", null);

                var dto = new PaymentDto
                {
                    Id = payment.Id,
                    UserId = payment.UserId,
                    Amount = payment.Amount,
                    Status = payment.Status.ToString(),
                    Date = payment.Date,
                    UpdatedAt = payment.UpdatedAt,
                    TransactionId = payment.TransactionId
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment {PaymentId}", id);
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<PaymentDto> Payments)> GetByUser(int userId)
        {
            try
            {
                var payments = await _ctx.Set<Payment>()
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.Date)
                    .Select(p => new PaymentDto
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Amount = p.Amount,
                        Status = p.Status.ToString(),
                        Date = p.Date,
                        UpdatedAt = p.UpdatedAt,
                        TransactionId = p.TransactionId
                    })
                    .ToListAsync();

                return (true, null, payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payments for user {UserId}", userId);
                return (false, ex.Message, new List<PaymentDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> UpdatePaymentStatus(string transactionId, PaymentStatus newStatus)
        {
            try
            {
                var payment = await _ctx.Set<Payment>()
                    .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

                if (payment == null)
                    return (false, "Payment not found");

                payment.Status = newStatus;
                payment.UpdatedAt = DateTime.UtcNow;

                await _ctx.SaveChangesAsync();

                _logger.LogInformation("Payment {TransactionId} status updated to {Status}", transactionId, newStatus);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update payment status for transaction {TransactionId}", transactionId);
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> UpgradeUserToPremium(int userId, int durationDays = 30)
        {
            try
            {
                var user = await _ctx.Set<User>().FindAsync(userId);
                if (user == null)
                    return (false, "User not found");

                user.SubscriptionStatus = SubscriptionStatus.Premium;
                user.SubscriptionExpiration = DateTime.UtcNow.AddDays(durationDays);

                await _ctx.SaveChangesAsync();

                _logger.LogInformation("User {UserId} upgraded to Premium until {Expiration}",
                    userId, user.SubscriptionExpiration);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upgrade user {UserId} to Premium", userId);
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
