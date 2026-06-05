using Edemly.Contracts.Calls;
using Edemly.Contracts.Payments;
using Edemly.Contracts.Remindings;
using Edemly.Client.Application.Users.Profile;
namespace Edemly.Client.Api
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

        Task<UserInfoDto> GetUserInfoAsync();

        Task<(bool Success, string? Error)> UpdateUserInfoAsync(UserProfileUpdateRequest request);

        Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(string filePath);

        Task<(bool Success, byte[]? ImageData, string? Error)> DownloadProfilePictureAsync(string pfpUrl);

        Task<string?> GetContactNoteAsync(int userId);

        Task<bool> SaveContactNoteAsync(int userId, string noteText);

        Task<bool> DeleteContactNoteAsync(int userId);

        Task<int> GetNotesCountAsync();

        Task<ChatDto?> CreateGroupChatAsync(string groupName, List<int> participantIds);

        Task<(bool Success, string? Error)> UpdateChatAsync(int chatId, string? name = null, string? description = null, string? iconUrl = null);

        Task<(bool Success, string? Url, string? Error)> UploadGroupIconAsync(int chatId, string filePath);

        Task<ChatDto?> GetChatByIdAsync(int chatId);

        Task<(bool Success, string? Url, string? FileName, string? Error)> UploadFileAsync(string filePath);

        Task<List<CallDto>> GetActiveCallsAsync();

        Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model);

        Task<List<RemindingDto>> GetMyRemindingsAsync();

        Task<(bool Success, string? Html, string? Error)> InitiatePaymentAsync(decimal amount);

        Task<List<PaymentDto>> GetPaymentHistoryAsync();

        Task<(bool Success, bool IsPaid, string? Error)> CheckPaymentStatusAsync(string orderId);

        Task<bool> DeleteRemindingAsync(int id);

        Task<bool> UpdateRemindingAsync(UpdateRemindingDto model);

        Task<bool> ToggleRemindingAsync(int id);
    }
}
