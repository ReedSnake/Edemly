using Edemly.Contracts.Notes;

namespace Edemly.Server.Api.Services
{
    public interface INoteService
    {
        Task<(bool Success, string? Error, NoteDto Note)> GetById(int id);
        Task<(bool Success, string? Error, List<NoteDto> Notes)> GetAll(int creatorId);
        Task<(bool Success, string? Error)> Create(int creatorId, CreateNoteDto model);
        Task<(bool Success, string? Error)> Update(UpdateNoteDto model);
        Task<(bool Success, string? Error)> Delete(int id);
        Task<(bool Success, string? Error, int Count)> GetCount(int creatorId);
        Task<(bool Success, string? Error)> DeleteByUser(int creatorId, int userId);
    }
}
