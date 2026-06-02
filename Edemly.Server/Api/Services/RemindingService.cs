using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Contracts.Remindings;
namespace Edemly.Server.Api.Services
{
    public class RemindingService : IRemindingService
    {
        private readonly ILogger<RemindingService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public RemindingService(ServerDbContext serverDb, ILogger<RemindingService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        // Create a new reminding
        public async Task<(bool Success, string? Error)> Create(int creatorId, CreateRemindingDto model)
        {
            _logger.LogWarning(model.Name);
            _logger.LogWarning(model.Type.ToString());
            _logger.LogWarning(model.Content);
            try
            {
                var reminding = new Reminding
                {
                    UserId = creatorId,
                    Content = model.Content,
                    CreatedAt = DateTime.UtcNow,
                    LastTime = model.LastTime,
                    ShouldNotify = model.ShouldNotify,
                    Name = model.Name,
                    Type = model.Type,
                    ShowTime = model.ShowTime
                };

                _ctx.Set<Reminding>().Add(reminding);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create reminding");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Update an existing reminding
        public async Task<(bool Success, string? Error)> Update(UpdateRemindingDto model)
        {
            try
            {
                var reminding = await _ctx.Set<Reminding>().FindAsync(model.Id);
                if (reminding == null)
                    return (false, "Reminding not found");

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


                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reminding");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> ToggleCompletion(int id)
        {
            try
            {
                var reminding = await _ctx.Set<Reminding>().FindAsync(id);
                UpdateRemindingDto model = new UpdateRemindingDto { Id = id, IsCompleted = !reminding.IsCompleted };
                await Update(model);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reminding");
                return (false, ex.Message);
            }
        }

        // Delete a reminding
        public async Task<(bool Success, string? Error)> Delete(int id)
        {
            try
            {
                var reminding = await _ctx.Set<Reminding>().FindAsync(id);
                if (reminding == null)
                    return (false, "Reminding not found");

                _ctx.Set<Reminding>().Remove(reminding);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete reminding");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get a single reminding by id
        public async Task<(bool Success, string? Error, RemindingDto Reminding)> GetById(int id)
        {
            try
            {
                var reminding = await _ctx.Set<Reminding>().FindAsync(id);
                if (reminding == null)
                    return (false, "Reminding not found", null!);

                var dto = new RemindingDto
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

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get reminding by id");
                return (false, ex.Message, null!);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get all remindings for a user
        public async Task<(bool Success, string? Error, List<RemindingDto> Remindings)> GetByUser(int userId)
        {
            try
            {
                var remindings = await _ctx.Set<Reminding>()
                    .Where(r => r.UserId == userId)
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

                return (true, null, remindings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get remindings for user");
                return (false, ex.Message, new List<RemindingDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> ConfirmReminding(int userId, int remindingId)
        {
            var reminding = await _ctx.Set<Reminding>()
                .Where(r => r.Id == remindingId && r.UserId == userId)
                .FirstOrDefaultAsync();

            if (reminding == null)
            {
                _logger.LogError("Reminding not found or doesnt belong to this user");
                return (false, "Reminding not found or doesnt belong to this user");
            }

            reminding.ShouldNotify = false;

            await _ctx.SaveChangesAsync();

            _logger.LogInformation(
                "User {UserId} confirmed reminding {RemId}, notifications disabled",
                userId, remindingId
            );
            return (true, null);
        }
    }
}
