using Edemly.Contracts.Remindings;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Remindings
{
    public class RemindingService : TenantAwareServiceBase, IRemindingService
    {
        private readonly ILogger<RemindingService> _logger;

        public RemindingService(ServerDbContext serverDbContext, ILogger<RemindingService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult> CreateAsync(int currentUserId, CreateRemindingDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = new Reminding
                {
                    UserId = currentUserId,
                    Content = request.Content,
                    CreatedAt = DateTime.UtcNow,
                    LastTime = request.LastTime,
                    ShouldNotify = request.ShouldNotify,
                    Name = request.Name,
                    Type = request.Type,
                    ShowTime = request.ShowTime
                };

                ctx.Set<Reminding>().Add(reminding);
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Reminding created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create reminding");
                return ServiceResult.Unexpected("Failed to create reminding");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int currentUserId, UpdateRemindingDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>().FirstOrDefaultAsync(item => item.Id == request.Id);
                if (reminding == null)
                {
                    return ServiceResult.NotFound("Reminding not found");
                }

                if (reminding.UserId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                if (!string.IsNullOrEmpty(request.Content))
                    reminding.Content = request.Content;

                if (!string.IsNullOrEmpty(request.Name))
                    reminding.Name = request.Name;

                if (request.LastTime.HasValue)
                    reminding.LastTime = request.LastTime.Value;

                if (request.ShouldNotify.HasValue)
                    reminding.ShouldNotify = request.ShouldNotify.Value;

                if (request.ShowTime.HasValue)
                    reminding.ShowTime = request.ShowTime.Value;

                if (request.IsCompleted.HasValue)
                    reminding.IsCompleted = request.IsCompleted.Value;

                if (request.Type.HasValue)
                    reminding.Type = request.Type.Value;

                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Reminding updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reminding");
                return ServiceResult.Unexpected("Failed to update reminding");
            }
        }

        public async Task<ServiceResult> ToggleCompletionAsync(int currentUserId, int remindingId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>().FirstOrDefaultAsync(item => item.Id == remindingId);
                if (reminding == null)
                {
                    return ServiceResult.NotFound("Reminding not found");
                }

                if (reminding.UserId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                reminding.IsCompleted = !reminding.IsCompleted;
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Reminding updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle reminding completion");
                return ServiceResult.Unexpected("Failed to update reminding");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int currentUserId, int remindingId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>().FirstOrDefaultAsync(item => item.Id == remindingId);
                if (reminding == null)
                {
                    return ServiceResult.NotFound("Reminding not found");
                }

                if (reminding.UserId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                ctx.Set<Reminding>().Remove(reminding);
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Reminding deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete reminding");
                return ServiceResult.Unexpected("Failed to delete reminding");
            }
        }

        public async Task<ServiceResult<RemindingDto>> GetByIdAsync(int currentUserId, int remindingId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == remindingId);

                if (reminding == null)
                {
                    return ServiceResult<RemindingDto>.NotFound("Reminding not found");
                }

                if (reminding.UserId != currentUserId)
                {
                    return ServiceResult<RemindingDto>.Forbidden();
                }

                return ServiceResult<RemindingDto>.Ok(RemindingMappings.ToDto(reminding));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get reminding by id {RemindingId}", remindingId);
                return ServiceResult<RemindingDto>.Unexpected("Failed to get reminding");
            }
        }

        public async Task<ServiceResult<List<RemindingDto>>> GetByUserAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var remindings = await ctx.Set<Reminding>()
                    .AsNoTracking()
                    .Where(r => r.UserId == currentUserId)
                    .OrderBy(r => r.LastTime)
                    .Select(RemindingMappings.Projection)
                    .ToListAsync();

                return ServiceResult<List<RemindingDto>>.Ok(remindings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get remindings for user");
                return ServiceResult<List<RemindingDto>>.Unexpected("Failed to get remindings");
            }
        }

        public async Task<ServiceResult> ConfirmRemindingAsync(int currentUserId, int remindingId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>()
                    .FirstOrDefaultAsync(item => item.Id == remindingId);

                if (reminding == null)
                {
                    _logger.LogWarning("Reminding {RemindingId} was not found during confirmation", remindingId);
                    return ServiceResult.NotFound("Reminding not found");
                }

                if (reminding.UserId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                reminding.ShouldNotify = false;
                await ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserId} confirmed reminding {RemId}, notifications disabled",
                    currentUserId,
                    remindingId);

                return ServiceResult.Ok("Reminding confirmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm reminding");
                return ServiceResult.Unexpected("Failed to confirm reminding");
            }
        }
    }
}