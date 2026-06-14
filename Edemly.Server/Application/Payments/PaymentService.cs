using Edemly.Contracts.Payments;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Payments
{
    public class PaymentService : TenantAwareServiceBase, IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ServerDbContext serverDbContext, ILogger<PaymentService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult> CreateAsync(int currentUserId, CreatePaymentDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var userExists = await ctx.Set<User>()
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == currentUserId);

                if (!userExists)
                {
                    return ServiceResult.NotFound("User not found");
                }

                var payment = new Payment
                {
                    UserId = currentUserId,
                    Amount = request.Amount,
                    Status = Enum.Parse<PaymentStatus>(request.Status),
                    Date = request.Date,
                    UpdatedAt = DateTime.UtcNow,
                    TransactionId = request.TransactionId
                };

                ctx.Set<Payment>().Add(payment);
                await ctx.SaveChangesAsync();

                _logger.LogInformation("Payment created for user {UserId}, Amount: {Amount}", currentUserId, request.Amount);
                return ServiceResult.Ok("Payment created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment for user {UserId}", currentUserId);
                return ServiceResult.Unexpected("Failed to create payment");
            }
        }

        public async Task<ServiceResult<PaymentDto>> GetByIdAsync(int paymentId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payment = await ctx.Set<Payment>()
                    .AsNoTracking()
                    .Where(p => p.Id == paymentId)
                    .Select(PaymentMappings.Projection)
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    return ServiceResult<PaymentDto>.NotFound("Payment not found");
                }

                return ServiceResult<PaymentDto>.Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payment {PaymentId}", paymentId);
                return ServiceResult<PaymentDto>.Unexpected("Failed to get payment");
            }
        }

        public async Task<ServiceResult<List<PaymentDto>>> GetByUserAsync(int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payments = await ctx.Set<Payment>()
                    .AsNoTracking()
                    .Where(p => p.UserId == targetUserId)
                    .OrderByDescending(p => p.Date)
                    .Select(PaymentMappings.Projection)
                    .ToListAsync();

                return ServiceResult<List<PaymentDto>>.Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get payments for user {UserId}", targetUserId);
                return ServiceResult<List<PaymentDto>>.Unexpected("Failed to get payments");
            }
        }

        public async Task<ServiceResult> UpdatePaymentStatusAsync(string transactionId, PaymentStatus newStatus)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var payment = await ctx.Set<Payment>()
                    .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

                if (payment == null)
                {
                    return ServiceResult.NotFound("Payment not found");
                }

                payment.Status = newStatus;
                payment.UpdatedAt = DateTime.UtcNow;

                await ctx.SaveChangesAsync();

                _logger.LogInformation("Payment {TransactionId} status updated to {Status}", transactionId, newStatus);
                return ServiceResult.Ok("Payment status updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update payment status for transaction {TransactionId}", transactionId);
                return ServiceResult.Unexpected("Failed to update payment status");
            }
        }

        public async Task<ServiceResult> MarkPaidAndUpgradeUserAsync(string transactionId, int durationDays = 30)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;
                var strategy = ctx.Database.CreateExecutionStrategy();

                ServiceResult? result = null;
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await ctx.Database.BeginTransactionAsync();

                    var payment = await ctx.Set<Payment>()
                        .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

                    if (payment == null)
                    {
                        result = ServiceResult.NotFound("Payment not found");
                        return;
                    }

                    var user = await ctx.Set<User>().FindAsync(payment.UserId);
                    if (user == null)
                    {
                        result = ServiceResult.NotFound("User not found");
                        return;
                    }

                    payment.Status = PaymentStatus.Paid;
                    payment.UpdatedAt = DateTime.UtcNow;
                    user.SubscriptionStatus = SubscriptionStatus.Premium;
                    user.SubscriptionExpiration = DateTime.UtcNow.AddDays(durationDays);

                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Payment {TransactionId} marked paid and user {UserId} upgraded to Premium until {Expiration}",
                        transactionId,
                        user.Id,
                        user.SubscriptionExpiration);

                    result = ServiceResult.Ok("Payment paid and user upgraded");
                });

                return result ?? ServiceResult.Unexpected("Failed to complete payment");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete paid payment for transaction {TransactionId}", transactionId);
                return ServiceResult.Unexpected("Failed to complete payment");
            }
        }

        public async Task<ServiceResult> UpgradeUserToPremiumAsync(int targetUserId, int durationDays = 30)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var user = await ctx.Set<User>().FindAsync(targetUserId);
                if (user == null)
                {
                    return ServiceResult.NotFound("User not found");
                }

                user.SubscriptionStatus = SubscriptionStatus.Premium;
                user.SubscriptionExpiration = DateTime.UtcNow.AddDays(durationDays);

                await ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserId} upgraded to Premium until {Expiration}",
                    targetUserId,
                    user.SubscriptionExpiration);
                return ServiceResult.Ok("User upgraded to Premium");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upgrade user {UserId} to Premium", targetUserId);
                return ServiceResult.Unexpected("Failed to upgrade subscription");
            }
        }
    }
}
