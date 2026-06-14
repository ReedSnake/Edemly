using Edemly.Contracts.Messages;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Messages
{
    public interface IMessageService
    {
        Task<ServiceResult<MessageDto>> GetByIdAsync(int currentUserId, int messageId);

        Task<ServiceResult<List<MessageDto>>> GetByChatAsync(int currentUserId, int chatId, int page, int pageSize);

        Task<ServiceResult<MessageDto>> GetLastByChatAsync(int currentUserId, int chatId);

        Task<ServiceResult> CreateAsync(int currentUserId, CreateMessageDto request);

        Task<ServiceResult> UpdateAsync(int currentUserId, UpdateMessageDto request);

        Task<ServiceResult> DeleteAsync(int requesterId, int messageId);
    }
}
