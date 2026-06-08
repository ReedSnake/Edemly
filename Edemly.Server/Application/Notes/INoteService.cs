using Edemly.Contracts.Notes;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Notes
{
    public interface INoteService
    {
        Task<ServiceResult<NoteDto>> GetContactNoteAsync(
            int currentUserId,
            int targetUserId);

        Task<ServiceResult<NoteDto>> SaveContactNoteAsync(
            int currentUserId,
            int targetUserId,
            SaveContactNoteDto request);

        Task<ServiceResult> DeleteContactNoteAsync(
            int currentUserId,
            int targetUserId);

        Task<ServiceResult<int>> GetCountAsync(int currentUserId);
    }
}