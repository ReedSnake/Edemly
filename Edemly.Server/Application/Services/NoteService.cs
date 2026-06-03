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
    public class NoteService : INoteService
    {
        private const int FreePlanLimit = 5;

        private readonly ILogger<NoteService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public NoteService(ServerDbContext serverDb, ILogger<NoteService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        public async Task<ServiceDataResult<NoteDto>> GetById(int currentUserId, int id)
        {
            try
            {
                var note = await GetOwnedNoteAsync(currentUserId, id);
                if (note == null)
                {
                    return ServiceDataResult<NoteDto>.Forbidden();
                }

                return ServiceDataResult<NoteDto>.Ok(ToDto(note));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get note by id");
                return ServiceDataResult<NoteDto>.NotFound(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceDataResult<List<NoteDto>>> GetAll(int currentUserId)
        {
            try
            {
                var notes = await _ctx.Set<Note>()
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
                return ServiceDataResult<List<NoteDto>>.NotFound(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceMessageResult> Create(int currentUserId, CreateNoteDto model)
        {
            try
            {
                var creator = await _ctx.Set<User>().FindAsync(currentUserId);
                if (creator == null)
                {
                    return ServiceMessageResult.BadRequest("Creator not found");
                }

                var isUnlimited = _isTenant
                    || creator.SubscriptionStatus == SubscriptionStatus.Premium
                    || creator.SubscriptionStatus == SubscriptionStatus.Vip;

                var existingCount = await _ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                if (!isUnlimited && existingCount >= FreePlanLimit)
                {
                    return ServiceMessageResult.BadRequest($"Note limit reached. Free plan allows up to {FreePlanLimit} notes.");
                }

                var existing = await _ctx.Set<Note>()
                    .FirstOrDefaultAsync(n => n.CreatorId == currentUserId && n.UserId == model.UserId);

                if (existing != null)
                {
                    existing.Content = model.Content;
                    await _ctx.SaveChangesAsync();
                    return ServiceMessageResult.Ok("Note created");
                }

                var note = new Note
                {
                    CreatorId = currentUserId,
                    UserId = model.UserId,
                    Content = model.Content
                };

                _ctx.Set<Note>().Add(note);
                await _ctx.SaveChangesAsync();

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
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceMessageResult> Update(int currentUserId, UpdateNoteDto model)
        {
            try
            {
                var note = await GetOwnedNoteAsync(currentUserId, model.Id);
                if (note == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                note.Content = model.Content;
                await _ctx.SaveChangesAsync();

                return ServiceMessageResult.Ok("Note updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update note");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceMessageResult> Delete(int currentUserId, int id)
        {
            try
            {
                var note = await GetOwnedNoteAsync(currentUserId, id);
                if (note == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                _ctx.Set<Note>().Remove(note);
                await _ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceDataResult<int>> GetCount(int currentUserId)
        {
            try
            {
                var count = await _ctx.Set<Note>().CountAsync(n => n.CreatorId == currentUserId);
                return ServiceDataResult<int>.Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get notes count");
                return ServiceDataResult<int>.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        public async Task<ServiceMessageResult> DeleteByUser(int currentUserId, int userId)
        {
            try
            {
                var note = await _ctx.Set<Note>()
                    .FirstOrDefaultAsync(n => n.CreatorId == currentUserId && n.UserId == userId);

                if (note == null)
                {
                    return ServiceMessageResult.BadRequest("Note not found");
                }

                _ctx.Set<Note>().Remove(note);
                await _ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Note deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete note by user");
                return ServiceMessageResult.BadRequest(ex.Message);
            }
            finally
            {
                DisposeTenantContext();
            }
        }

        private async Task<Note?> GetOwnedNoteAsync(int currentUserId, int noteId)
        {
            return await _ctx.Set<Note>()
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

        private void DisposeTenantContext()
        {
            if (_isTenant)
            {
                _ctx.Dispose();
            }
        }
    }
}
