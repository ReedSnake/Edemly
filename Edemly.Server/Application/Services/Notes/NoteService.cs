using Edemly.Contracts.Notes;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
{
    public class NoteService : TenantAwareServiceBase, INoteService
    {
        private const int FreePlanLimit = 5;

        private readonly ILogger<NoteService> _logger;

        public NoteService(ServerDbContext serverDbContext, ILogger<NoteService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult<NoteDto>> GetByIdAsync(int currentUserId, int noteId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == noteId);

                if (note == null)
                {
                    return ServiceResult<NoteDto>.NotFound("Note not found");
                }

                if (note.CreatorId != currentUserId)
                {
                    return ServiceResult<NoteDto>.Forbidden();
                }

                return ServiceResult<NoteDto>.Ok(NoteMappings.ToDto(note));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get note by id {NoteId}", noteId);
                return ServiceResult<NoteDto>.Unexpected("Failed to get note");
            }
        }

        public async Task<ServiceResult<List<NoteDto>>> GetAllAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var notes = await ctx.Set<Note>()
                    .AsNoTracking()
                    .Where(n => n.CreatorId == currentUserId)
                    .Select(NoteMappings.Projection)
                    .ToListAsync();

                return ServiceResult<List<NoteDto>>.Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes");
                return ServiceResult<List<NoteDto>>.Unexpected("Failed to get notes");
            }
        }

        public async Task<ServiceResult> CreateAsync(int currentUserId, CreateNoteDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var creator = await ctx.Set<User>().FindAsync(currentUserId);
                if (creator == null)
                {
                    return ServiceResult.NotFound("User not found");
                }

                var isUnlimited = IsTenantRequest
                    || creator.SubscriptionStatus == SubscriptionStatus.Premium
                    || creator.SubscriptionStatus == SubscriptionStatus.Vip;

                var existingCount = await ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                if (!isUnlimited && existingCount >= FreePlanLimit)
                {
                    return ServiceResult.Conflict($"Note limit reached. Free plan allows up to {FreePlanLimit} notes.");
                }

                var existing = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(n => n.CreatorId == currentUserId && n.UserId == request.UserId);

                if (existing != null)
                {
                    existing.Content = request.Content;
                    await ctx.SaveChangesAsync();
                    return ServiceResult.Ok("Note created");
                }

                var note = new Note
                {
                    CreatorId = currentUserId,
                    UserId = request.UserId,
                    Content = request.Content
                };

                ctx.Set<Note>().Add(note);
                await ctx.SaveChangesAsync();

                return ServiceResult.Ok("Note created");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error while creating note");
                return ServiceResult.Conflict("Database update error. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create note");
                return ServiceResult.Unexpected("Failed to create note");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int currentUserId, UpdateNoteDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>().FirstOrDefaultAsync(item => item.Id == request.Id);
                if (note == null)
                {
                    return ServiceResult.NotFound("Note not found");
                }

                if (note.CreatorId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                note.Content = request.Content;
                await ctx.SaveChangesAsync();

                return ServiceResult.Ok("Note updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update note");
                return ServiceResult.Unexpected("Failed to update note");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int currentUserId, int noteId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>().FirstOrDefaultAsync(item => item.Id == noteId);
                if (note == null)
                {
                    return ServiceResult.NotFound("Note not found");
                }

                if (note.CreatorId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                ctx.Set<Note>().Remove(note);
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note");
                return ServiceResult.Unexpected("Failed to delete note");
            }
        }

        public async Task<ServiceResult<int>> GetCountAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var count = await ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                return ServiceResult<int>.Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes count");
                return ServiceResult<int>.Unexpected("Failed to get notes count");
            }
        }

        public async Task<ServiceResult> DeleteByUserAsync(int currentUserId, int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(noteItem => noteItem.CreatorId == currentUserId && noteItem.UserId == targetUserId);

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
                _logger.LogError(ex, "Failed to delete note by user");
                return ServiceResult.Unexpected("Failed to delete note");
            }
        }
    }
}