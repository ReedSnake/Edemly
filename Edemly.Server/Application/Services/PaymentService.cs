using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Contracts.Payments;

namespace Edemly.Server.Api.Services
{
    public class PaymentService : TenantAwareServiceBase, IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ServerDbContext serverDb, ILogger<PaymentService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceMessageResult> Create(int userId, CreatePaymentDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>().FindAsync(userId);
                if (user == null)
                {
                    return ServiceMessageResult.BadRequest("User not found");
                }

                var payment = new Payment
                {
                    UserId = userId,
                    Amount = model.Amount,
                    Status = Enum.Parse<PaymentStatus>(model.Status),
                    Date = model.Date,
                    UpdatedAt = DateTime.UtcNow,
                    TransactionId = model.TransactionId
                };

                ctx.Set<Payment>().Add(payment);
                await ctx.SaveChangesAsync();

                _logger.LogInformation("Payment created for user {UserId}, Amount: {Amount}", userId, model.Amount);
                return ServiceMessageResult.Ok("Payment created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment for user {UserId}", userId);
                return ServiceMessageResult.Unexpected("Failed to create payment");
            }
        }

        public async Task<ServiceDataResult<PaymentDto>> GetById(int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payment = await ctx.Set<Payment>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (payment == null)
                {
                    return ServiceDataResult<PaymentDto>.NotFound("Payment not found");
                }

                return ServiceDataResult<PaymentDto>.Ok(ToDto(payment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment {PaymentId}", id);
                return ServiceDataResult<PaymentDto>.Unexpected("Failed to get payment");
            }
        }

        public async Task<ServiceDataResult<List<PaymentDto>>> GetByUser(int userId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payments = await ctx.Set<Payment>()
                    .AsNoTracking()
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

                return ServiceDataResult<List<PaymentDto>>.Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payments for user {UserId}", userId);
                return ServiceDataResult<List<PaymentDto>>.Unexpected("Failed to get payments");
            }
        }

        public async Task<ServiceMessageResult> UpdatePaymentStatus(string transactionId, PaymentStatus newStatus)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payment = await ctx.Set<Payment>()
                    .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

                if (payment == null)
                {
                    return ServiceMessageResult.BadRequest("Payment not found");
                }

                payment.Status = newStatus;
                payment.UpdatedAt = DateTime.UtcNow;

                await ctx.SaveChangesAsync();

                _logger.LogInformation("Payment {TransactionId} status updated to {Status}", transactionId, newStatus);
                return ServiceMessageResult.Ok("Payment status updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update payment status for transaction {TransactionId}", transactionId);
                return ServiceMessageResult.Unexpected("Failed to update payment status");
            }
        }

        public async Task<ServiceMessageResult> UpgradeUserToPremium(int userId, int durationDays = 30)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>().FindAsync(userId);
                if (user == null)
                {
                    return ServiceMessageResult.BadRequest("User not found");
                }

                user.SubscriptionStatus = SubscriptionStatus.Premium;
                user.SubscriptionExpiration = DateTime.UtcNow.AddDays(durationDays);

                await ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserId} upgraded to Premium until {Expiration}",
                    userId,
                    user.SubscriptionExpiration);
                return ServiceMessageResult.Ok("User upgraded to Premium");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upgrade user {UserId} to Premium", userId);
                return ServiceMessageResult.Unexpected("Failed to upgrade subscription");
            }
        }

        private static PaymentDto ToDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                Date = payment.Date,
                UpdatedAt = payment.UpdatedAt,
                TransactionId = payment.TransactionId
            };
        }
    }
}
