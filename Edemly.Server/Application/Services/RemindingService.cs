using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Services
{
    public class RemindingService : TenantAwareServiceBase, IRemindingService
    {
        private readonly ILogger<RemindingService> _logger;

        public RemindingService(ServerDbContext serverDb, ILogger<RemindingService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceMessageResult> Create(int currentUserId, CreateRemindingDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = new Reminding
                {
                    UserId = currentUserId,
                    Content = model.Content,
                    CreatedAt = DateTime.UtcNow,
                    LastTime = model.LastTime,
                    ShouldNotify = model.ShouldNotify,
                    Name = model.Name,
                    Type = model.Type,
                    ShowTime = model.ShowTime
                };

                ctx.Set<Reminding>().Add(reminding);
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Reminding created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create reminding");
                return ServiceMessageResult.Unexpected("Failed to create reminding");
            }
        }

        public async Task<ServiceMessageResult> Update(int currentUserId, UpdateRemindingDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await GetOwnedRemindingAsync(ctx, currentUserId, model.Id);
                if (reminding == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                if (!string.IsNullOrEmpty(model.Content))
                    reminding.Content = model.Content;

                if (!string.IsNullOrEmpty(model.Name))
                    reminding.Name = model.Name;

                if (model.LastTime.HasValue)
                    reminding.LastTime = model.LastTime.Value;

                if (model.ShouldNotify.HasValue)
                    reminding.ShouldNotify = model.ShouldNotify.Value;

                if (model.ShowTime.HasValue)
                    reminding.ShowTime = model.ShowTime.Value;

                if (model.IsCompleted.HasValue)
                    reminding.IsCompleted = model.IsCompleted.Value;

                if (model.Type.HasValue)
                    reminding.Type = model.Type.Value;

                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Reminding updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reminding");
                return ServiceMessageResult.Unexpected("Failed to update reminding");
            }
        }

        public async Task<ServiceMessageResult> ToggleCompletion(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await GetOwnedRemindingAsync(ctx, currentUserId, id);
                if (reminding == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                reminding.IsCompleted = !reminding.IsCompleted;
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Reminding updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle reminding completion");
                return ServiceMessageResult.Unexpected("Failed to update reminding");
            }
        }

        public async Task<ServiceMessageResult> Delete(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await GetOwnedRemindingAsync(ctx, currentUserId, id);
                if (reminding == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                ctx.Set<Reminding>().Remove(reminding);
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Reminding deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete reminding");
                return ServiceMessageResult.Unexpected("Failed to delete reminding");
            }
        }

        public async Task<ServiceDataResult<RemindingDto>> GetById(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id && r.UserId == currentUserId);

                if (reminding == null)
                {
                    return ServiceDataResult<RemindingDto>.Forbidden();
                }

                return ServiceDataResult<RemindingDto>.Ok(ToDto(reminding));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get reminding by id");
                return ServiceDataResult<RemindingDto>.Unexpected("Failed to get reminding");
            }
        }

        public async Task<ServiceDataResult<List<RemindingDto>>> GetByUser(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var remindings = await ctx.Set<Reminding>()
                    .AsNoTracking()
                    .Where(r => r.UserId == currentUserId)
                    .OrderBy(r => r.LastTime)
                    .Select(r => new RemindingDto
                    {
                        Id = r.Id,
                        UserId = r.UserId,
                        Content = r.Content,
                        CreatedAt = r.CreatedAt,
                        LastTime = r.LastTime,
                        ShouldNotify = r.ShouldNotify,
                        Type = r.Type,
                        Name = r.Name,
                        ShowTime = r.ShowTime,
                        IsCompleted = r.IsCompleted
                    })
                    .ToListAsync();

                return ServiceDataResult<List<RemindingDto>>.Ok(remindings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get remindings for user");
                return ServiceDataResult<List<RemindingDto>>.Unexpected("Failed to get remindings");
            }
        }

        public async Task<ServiceMessageResult> ConfirmReminding(int userId, int remindingId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var reminding = await ctx.Set<Reminding>()
                    .FirstOrDefaultAsync(r => r.Id == remindingId && r.UserId == userId);

                if (reminding == null)
                {
                    _logger.LogError("Reminding not found or doesnt belong to this user");
                    return ServiceMessageResult.BadRequest("Reminding not found or doesnt belong to this user");
                }

                reminding.ShouldNotify = false;
                await ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "User {UserId} confirmed reminding {RemId}, notifications disabled",
                    userId,
                    remindingId);

                return ServiceMessageResult.Ok("Reminding confirmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm reminding");
                return ServiceMessageResult.Unexpected("Failed to confirm reminding");
            }
        }

        private static Task<Reminding?> GetOwnedRemindingAsync(DbContext ctx, int currentUserId, int remindingId)
        {
            return ctx.Set<Reminding>()
                .FirstOrDefaultAsync(r => r.Id == remindingId && r.UserId == currentUserId);
        }

        private static RemindingDto ToDto(Reminding reminding)
        {
            return new RemindingDto
            {
                Id = reminding.Id,
                UserId = reminding.UserId,
                Content = reminding.Content,
                CreatedAt = reminding.CreatedAt,
                LastTime = reminding.LastTime,
                ShouldNotify = reminding.ShouldNotify,
                Type = reminding.Type,
                Name = reminding.Name,
                ShowTime = reminding.ShowTime,
                IsCompleted = reminding.IsCompleted
            };
        }
    }
}
