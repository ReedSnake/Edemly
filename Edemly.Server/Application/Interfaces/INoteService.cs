using Edemly.Contracts.Notes;

namespace Edemly.Server.Api.Services
{
    public interface INoteService
    {
        Task<ServiceDataResult<NoteDto>> GetById(int currentUserId, int id);
        Task<ServiceDataResult<List<NoteDto>>> GetAll(int currentUserId);
        Task<ServiceMessageResult> Create(int currentUserId, CreateNoteDto model);
        Task<ServiceMessageResult> Update(int currentUserId, UpdateNoteDto model);
        Task<ServiceMessageResult> Delete(int currentUserId, int id);
        Task<ServiceDataResult<int>> GetCount(int currentUserId);
        Task<ServiceMessageResult> DeleteByUser(int currentUserId, int userId);
    }
}
