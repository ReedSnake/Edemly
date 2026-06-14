using Edemly.Contracts.Notes;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Notes
{
    public class NoteService : TenantAwareServiceBase, INoteService
    {
        private const int FreePlanLimit = 5;

        private readonly ILogger<NoteService> _logger;

        public NoteService(
            ServerDbContext serverDbContext,
            ILogger<NoteService> logger,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult<NoteDto>> GetContactNoteAsync(
            int currentUserId,
            int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>()
                    .AsNoTracking()
                    .Where(n =>
                        n.CreatorId == currentUserId &&
                        n.TargetUserId == targetUserId)
                    .Select(NoteMappings.Projection)
                    .FirstOrDefaultAsync();

                if (note == null)
                {
                    return ServiceResult<NoteDto>.NotFound("Note not found");
                }

                return ServiceResult<NoteDto>.Ok(note);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to get contact note. CurrentUserId: {CurrentUserId}, TargetUserId: {TargetUserId}",
                    currentUserId,
                    targetUserId);

                return ServiceResult<NoteDto>.Unexpected("Failed to get note");
            }
        }

        public async Task<ServiceResult<NoteDto>> SaveContactNoteAsync(
            int currentUserId,
            int targetUserId,
            SaveContactNoteDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var creator = await ctx.Set<User>().FindAsync(currentUserId);
                if (creator == null)
                {
                    return ServiceResult<NoteDto>.NotFound("User not found");
                }

                var targetUserExists = await ctx.Set<User>()
                    .AnyAsync(u => u.Id == targetUserId);

                if (!targetUserExists)
                {
                    return ServiceResult<NoteDto>.NotFound("Target user not found");
                }

                var note = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(n =>
                        n.CreatorId == currentUserId &&
                        n.TargetUserId == targetUserId);

                if (note == null)
                {
                    var isUnlimited = IsTenantRequest
                        || creator.SubscriptionStatus == SubscriptionStatus.Premium
                        || creator.SubscriptionStatus == SubscriptionStatus.Vip;

                    var existingCount = await ctx.Set<Note>()
                        .CountAsync(n => n.CreatorId == currentUserId);

                    if (!isUnlimited && existingCount >= FreePlanLimit)
                    {
                        return ServiceResult<NoteDto>.Conflict(
                            $"Note limit reached. Free plan allows up to {FreePlanLimit} notes.");
                    }

                    note = new Note
                    {
                        CreatorId = currentUserId,
                        TargetUserId = targetUserId,
                        Content = request.Content
                    };

                    ctx.Set<Note>().Add(note);
                }
                else
                {
                    note.Content = request.Content;
                }

                await ctx.SaveChangesAsync();

                return ServiceResult<NoteDto>.Ok(NoteMappings.ToDto(note));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error while saving contact note");
                return ServiceResult<NoteDto>.Conflict("Database update error. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save contact note. CurrentUserId: {CurrentUserId}, TargetUserId: {TargetUserId}",
                    currentUserId,
                    targetUserId);

                return ServiceResult<NoteDto>.Unexpected("Failed to save note");
            }
        }

        public async Task<ServiceResult> DeleteContactNoteAsync(
            int currentUserId,
            int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(n =>
                        n.CreatorId == currentUserId &&
                        n.TargetUserId == targetUserId);

                if (note == null)
                {
                    return ServiceResult.NotFound("Note not found");
                }

                ctx.Set<Note>().Remove(note);
                await ctx.SaveChangesAsync();

                return ServiceResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete contact note. CurrentUserId: {CurrentUserId}, TargetUserId: {TargetUserId}",
                    currentUserId,
                    targetUserId);

                return ServiceResult.Unexpected("Failed to delete note");
            }
        }

        public async Task<ServiceResult<int>> GetCountAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var count = await ctx.Set<Note>()
                    .CountAsync(n => n.CreatorId == currentUserId);

                return ServiceResult<int>.Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes count");
                return ServiceResult<int>.Unexpected("Failed to get notes count");
            }
        }
    }
}
