using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Utils;
using Edemly.Contracts.Notes;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Services;

namespace Edemly.Server.Api.Services
{
    public class NoteService : TenantAwareServiceBase, INoteService
    {
        private const int FreePlanLimit = 5;

        private readonly ILogger<NoteService> _logger;

        public NoteService(ServerDbContext serverDb, ILogger<NoteService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceDataResult<NoteDto>> GetById(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await GetOwnedNoteAsync(ctx, currentUserId, id);
                if (note == null)
                {
                    return ServiceDataResult<NoteDto>.Forbidden();
                }

                return ServiceDataResult<NoteDto>.Ok(ToDto(note));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get note by id");
                return ServiceDataResult<NoteDto>.Unexpected("Failed to get note");
            }
        }

        public async Task<ServiceDataResult<List<NoteDto>>> GetAll(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var notes = await ctx.Set<Note>()
                    .AsNoTracking()
                    .Where(n => n.CreatorId == currentUserId)
                    .Select(n => new NoteDto
                    {
                        Id = n.Id,
                        UserId = n.UserId,
                        CreatorId = n.CreatorId,
                        Content = n.Content
                    })
                    .ToListAsync();

                return ServiceDataResult<List<NoteDto>>.Ok(notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes");
                return ServiceDataResult<List<NoteDto>>.Unexpected("Failed to get notes");
            }
        }

        public async Task<ServiceMessageResult> Create(int currentUserId, CreateNoteDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var creator = await ctx.Set<User>().FindAsync(currentUserId);
                if (creator == null)
                {
                    return ServiceMessageResult.BadRequest("Creator not found");
                }

                var isUnlimited = IsTenantRequest
                    || creator.SubscriptionStatus == SubscriptionStatus.Premium
                    || creator.SubscriptionStatus == SubscriptionStatus.Vip;

                var existingCount = await ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                if (!isUnlimited && existingCount >= FreePlanLimit)
                {
                    return ServiceMessageResult.BadRequest($"Note limit reached. Free plan allows up to {FreePlanLimit} notes.");
                }

                var existing = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(n => n.CreatorId == currentUserId && n.UserId == model.UserId);

                if (existing != null)
                {
                    existing.Content = model.Content;
                    await ctx.SaveChangesAsync();
                    return ServiceMessageResult.Ok("Note created");
                }

                var note = new Note
                {
                    CreatorId = currentUserId,
                    UserId = model.UserId,
                    Content = model.Content
                };

                ctx.Set<Note>().Add(note);
                await ctx.SaveChangesAsync();

                return ServiceMessageResult.Ok("Note created");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error while creating note");
                return ServiceMessageResult.BadRequest("Database update error. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create note");
                return ServiceMessageResult.Unexpected("Failed to create note");
            }
        }

        public async Task<ServiceMessageResult> Update(int currentUserId, UpdateNoteDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await GetOwnedNoteAsync(ctx, currentUserId, model.Id);
                if (note == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                note.Content = model.Content;
                await ctx.SaveChangesAsync();

                return ServiceMessageResult.Ok("Note updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update note");
                return ServiceMessageResult.Unexpected("Failed to update note");
            }
        }

        public async Task<ServiceMessageResult> Delete(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await GetOwnedNoteAsync(ctx, currentUserId, id);
                if (note == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                ctx.Set<Note>().Remove(note);
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note");
                return ServiceMessageResult.Unexpected("Failed to delete note");
            }
        }

        public async Task<ServiceDataResult<int>> GetCount(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var count = await ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                return ServiceDataResult<int>.Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes count");
                return ServiceDataResult<int>.Unexpected("Failed to get notes count");
            }
        }

        public async Task<ServiceMessageResult> DeleteByUser(int currentUserId, int userId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var note = await ctx.Set<Note>()
                    .FirstOrDefaultAsync(n => n.CreatorId == currentUserId && n.UserId == userId);

                if (note == null)
                {
                    return ServiceMessageResult.BadRequest("Note not found");
                }

                ctx.Set<Note>().Remove(note);
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note by user");
                return ServiceMessageResult.Unexpected("Failed to delete note");
            }
        }

        private static Task<Note?> GetOwnedNoteAsync(DbContext ctx, int currentUserId, int noteId)
        {
            return ctx.Set<Note>()
                .FirstOrDefaultAsync(n => n.Id == noteId && n.CreatorId == currentUserId);
        }

        private static NoteDto ToDto(Note note)
        {
            return new NoteDto
            {
                Id = note.Id,
                UserId = note.UserId,
                CreatorId = note.CreatorId,
                Content = note.Content
            };
        }
    }
}
