using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using uchat_server.Api.DTOs;
using uchat_server.Data;
using uchat_server.Data.Entities;
using uchat_server.Utils;
using static uchat_server.Api.DTOs.NoteDtos;
using uchat_server.Api.Middleware;
using uchat_server.Services;

namespace uchat_server.Api.Services
{
    public class NoteService : INoteService
    {
        private readonly ILogger<NoteService> _logger;
        private const int FREE_PLAN_LIMIT = 5;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public NoteService(ServerDbContext serverDb, ILogger<NoteService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        // Get a note by id
        public async Task<(bool Success, string? Error, NoteGetDto Note)> GetById(int id)
        {
            try
            {
                var note = await _ctx.Set<Note>().FindAsync(id);
                if (note == null)
                    return (false, "Note not found", null!);

                var dto = new NoteGetDto
                {
                    Id = note.Id,
                    UserId = note.UserId,
                    CreatorId = note.CreatorId,
                    Content = note.Content
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get note by id");
                return (false, ex.Message, null!);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Get all notes created by a user
        public async Task<(bool Success, string? Error, List<NoteGetDto> Notes)> GetAll(int creatorId)
        {
            try
            {
                var notes = await _ctx.Set<Note>()
                    .Where(n => n.CreatorId == creatorId)
                    .Select(n => new NoteGetDto
                    {
                        Id = n.Id,
                        UserId = n.UserId,
                        CreatorId = n.CreatorId,
                        Content = n.Content
                    })
                    .ToListAsync();

                return (true, null, notes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes");
                return (false, ex.Message, new List<NoteGetDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Create a new note
        public async Task<(bool Success, string? Error)> Create(int creatorId, NoteCreateDto model)
        {
            try
            {
                // Enforce per-creator limit for free users
                var creator = await _ctx.Set<User>().FindAsync(creatorId);
                if (creator == null)
                    return (false, "Creator not found");

                // If we're operating in a tenant (company) DB context, treat tenant installs as unlimited.
                // Otherwise allow unlimited only for Premium/Vip users.
                var isUnlimited = _isTenant
                    || creator.SubscriptionStatus == SubscriptionStatus.Premium
                    || creator.SubscriptionStatus == SubscriptionStatus.Vip;

                var existingCount = await _ctx.Set<Note>().CountAsync(n => n.CreatorId == creatorId);
                if (!isUnlimited && existingCount >= FREE_PLAN_LIMIT)
                {
                    return (false, $"Note limit reached. Free plan allows up to {FREE_PLAN_LIMIT} notes.");
                }

                // If a note for same (creatorId,userId) exists - update it instead of creating duplicate
                var existing = await _ctx.Set<Note>().FirstOrDefaultAsync(n => n.CreatorId == creatorId && n.UserId == model.UserId);
                if (existing != null)
                {
                    existing.Content = model.Content;
                    await _ctx.SaveChangesAsync();
                    return (true, null);
                }

                var note = new Note
                {
                    CreatorId = creatorId,
                    UserId = model.UserId,
                    Content = model.Content
                };

                _ctx.Set<Note>().Add(note);
                await _ctx.SaveChangesAsync();

                return (true, null);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error while creating note");
                return (false, "Database update error. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create note");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Update a note
        public async Task<(bool Success, string? Error)> Update(NoteUpdateDto model)
        {
            try
            {
                var note = await _ctx.Set<Note>().FindAsync(model.Id);
                if (note == null)
                    return (false, "Note not found");

                note.Content = model.Content;
                await _ctx.SaveChangesAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update note");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Delete a note
        public async Task<(bool Success, string? Error)> Delete(int id)
        {
            try
            {
                var note = await _ctx.Set<Note>().FindAsync(id);
                if (note == null)
                    return (false, "Note not found");

                _ctx.Set<Note>().Remove(note);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, int Count)> GetCount(int creatorId)
        {
            try
            {
                var count = await _ctx.Set<Note>().CountAsync(n => n.CreatorId == creatorId);
                return (true, null, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes count");
                return (false, ex.Message, 0);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> DeleteByUser(int creatorId, int userId)
        {
            try
            {
                var note = await _ctx.Set<Note>().FirstOrDefaultAsync(n => n.CreatorId == creatorId && n.UserId == userId);
                if (note == null)
                    return (false, "Note not found");

                _ctx.Set<Note>().Remove(note);
                await _ctx.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note by user");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
