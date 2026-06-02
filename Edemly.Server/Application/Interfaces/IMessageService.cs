namespace Edemly.Server.Api.Services
{
    public interface IMessageService
    {
        Task<(bool Success, string? Error, MessageDto Message)> GetById(int id);
        Task<(bool Success, string? Error, List<MessageDto> Messages)> GetByChat(int chatId, int page, int pageSize);
        Task<(bool Success, string? Error, MessageDto Message)> GetLastByChat(int chatId);
        Task<(bool Success, string? Error)> Create(int senderId, CreateMessageDto model);
        Task<(bool Success, string? Error)> Update(UpdateMessageDto model);
        Task<(bool Success, string? Error)> Delete(int id);
    }
}
