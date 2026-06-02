using System.Collections.Generic;
using System.Threading.Tasks;
using Edemly.Client.DTOs;
using Edemly.Contracts.Calls;
using Edemly.Contracts.Remindings;
using Edemly.Contracts.Payments;
using Edemly.Contracts.Users;
using Edemly.Contracts.Chats;
using Edemly.Contracts.ChatMembers;
namespace Edemly.Client.Services.Api
{
    public interface IApiService
    {
        void SetAuthToken(string token);
        Task<List<UserDto>> SearchUsersAsync(string query);
        Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int page = 1, int pageSize = 50);
        Task<List<ChatDto>> GetMyChatsAsync();
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<ChatDto?> CreateOrGetPrivateChatAsync(int userId);
        Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId);
        Task<UserInfoDto> GetUserInfo();
        Task<bool> UpdateUserInfo(string? phoneNumber, string? description, string? pfpUrl, string? name);

        // Для роботи зі світлинами
        Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(string filePath);
        Task<(bool Success, byte[]? ImageData, string? Error)> DownloadProfilePictureAsync(string pfpUrl);

        // Методи для роботи з нотатками (максимум можна створити 5 нотаток)
        Task<string?> GetContactNoteAsync(int userId);
        Task<bool> SaveContactNoteAsync(int userId, string noteText);
        Task<bool> DeleteContactNoteAsync(int userId);
        Task<int> GetNotesCountAsync();

        // ✅ ДОДАНО: Метод для створення групового чату
        Task<ChatDto?> CreateGroupChatAsync(string groupName, List<int> participantIds);

        // ✅ ДОДАНО: Методи для оновлення чату та завантаження іконки групи
        Task<(bool Success, string? Error)> UpdateChatAsync(int chatId, string? name = null, string? description = null, string? iconUrl = null);
        Task<(bool Success, string? Url, string? Error)> UploadGroupIconAsync(int chatId, string filePath);
        Task<ChatDto?> GetChatByIdAsync(int chatId);

        // Метод для завантаження файлів у повідомлення
        Task<(bool Success, string? Url, string? FileName, string? Error)> UploadFileAsync(string filePath);

        // Active calls
        Task<List<CallDto>> GetActiveCallsAsync();

        
        Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model);

        Task<List<RemindingDto>> GetMyRemindingsAsync();

        // Payments
        Task<(bool Success, string? Html, string? Error)> InitiatePaymentAsync(decimal amount);
        Task<List<PaymentDto>> GetPaymentHistoryAsync();
        Task<(bool Success, bool IsPaid, string? Error)> CheckPaymentStatusAsync(string orderId);
        Task<bool> DeleteRemindingAsync(int id);
        Task<bool> UpdateRemindingAsync(UpdateRemindingDto model);
        Task<bool> ToggleRemindingAsync(int id);
    }
}
