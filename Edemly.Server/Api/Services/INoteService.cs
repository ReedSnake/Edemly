using static uchat_server.Api.DTOs.NoteDtos;

namespace uchat_server.Api.Services
{
    public interface INoteService
    {
        Task<(bool Success, string? Error, NoteGetDto Note)> GetById(int id);
        Task<(bool Success, string? Error, List<NoteGetDto> Notes)> GetAll(int creatorId);
        Task<(bool Success, string? Error)> Create(int creatorId, NoteCreateDto model);
        Task<(bool Success, string? Error)> Update(NoteUpdateDto model);
        Task<(bool Success, string? Error)> Delete(int id);
        Task<(bool Success, string? Error, int Count)> GetCount(int creatorId);
        Task<(bool Success, string? Error)> DeleteByUser(int creatorId, int userId);
    }
}
