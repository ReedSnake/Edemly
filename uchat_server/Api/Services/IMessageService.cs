using static uchat_server.Api.DTOs.MessageDtos;

namespace uchat_server.Api.Services
{
    public interface IMessageService
    {
        Task<(bool Success, string? Error, MessageGetDto Message)> GetById(int id);
        Task<(bool Success, string? Error, List<MessageGetDto> Messages)> GetByChat(int chatId, int page, int pageSize);
        Task<(bool Success, string? Error, MessageGetDto Message)> GetLastByChat(int chatId);
        Task<(bool Success, string? Error)> Create(int senderId, MessageCreateDto model);
        Task<(bool Success, string? Error)> Update(MessageUpdateDto model);
        Task<(bool Success, string? Error)> Delete(int id);
    }
}
