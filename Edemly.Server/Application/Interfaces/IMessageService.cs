namespace Edemly.Server.Api.Services
{
    public interface IMessageService
    {
        Task<ServiceDataResult<MessageDto>> GetById(int id);
        Task<ServiceDataResult<List<MessageDto>>> GetByChat(int currentUserId, int chatId, int page, int pageSize);
        Task<ServiceDataResult<MessageDto>> GetLastByChat(int currentUserId, int chatId);
        Task<ServiceMessageResult> Create(int senderId, CreateMessageDto model);
        Task<ServiceMessageResult> Update(int currentUserId, UpdateMessageDto model);
        Task<ServiceMessageResult> Delete(int currentUserId, int id);
    }
}
