using Edemly.Contracts.Notes;

namespace Edemly.Server.Api.Services
{
    public interface INoteService
    {
        Task<ServiceResult<NoteDto>> GetByIdAsync(int currentUserId, int noteId);

        Task<ServiceResult<List<NoteDto>>> GetAllAsync(int currentUserId);

        Task<ServiceResult> CreateAsync(int currentUserId, CreateNoteDto request);

        Task<ServiceResult> UpdateAsync(int currentUserId, UpdateNoteDto request);

        Task<ServiceResult> DeleteAsync(int currentUserId, int noteId);

        Task<ServiceResult<int>> GetCountAsync(int currentUserId);

        Task<ServiceResult> DeleteByUserAsync(int currentUserId, int targetUserId);
    }
}