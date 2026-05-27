using System;
using System.Threading.Tasks;
using uchat.DTOs;

namespace uchat.Services
{
    public interface IHubService : IDisposable
    {
        /// <summary>
        /// Подія отримання нового повідомлення
        /// </summary>
        event Action<MessageDto>? MessageReceived;

        /// <summary>
        /// Подія оновлення повідомлення
        /// </summary>
        event Action<MessageDto>? MessageUpdated;

        /// <summary>
        /// Подія видалення повідомлення
        /// </summary>
        event Action<int, int>? MessageDeleted; // messageId, chatId

        /// <summary>
        /// Подія зміни стану підключення
        /// </summary>
        event Action<bool>? ConnectionStateChanged;

        /// <summary>
        /// Подія створення нової групи
        /// </summary>
        event Action<int>? GroupCreated; // chatId

        /// <summary>
        /// Подія оновлення групи (назва, опис, іконка)
        /// </summary>
        event Action<int, string?, string?, string?>? GroupUpdated; // chatId, name, description, iconUrl

        /// <summary>
        /// Подія зміни онлайн-статусу користувача
        /// </summary>
        event Action<int, bool, DateTime?>? UserStatusChanged; // userId, isOnline, lastSeen

        /// <summary>
        /// Подія оновлення профілю користувача
        /// </summary>
        event Action<int, string>? ProfileUpdated; // userId, newPfpUrl

        /// <summary>
        /// Чи підключено до хабу
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Підключитися до хабу
        /// </summary>
        Task<bool> ConnectAsync(string token);

        /// <summary>
        /// Відключитися від хабу
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Відправити повідомлення
        /// </summary>
        Task<bool> SendMessageAsync(MessageCreateDto message);

        /// <summary>
        /// Оновити повідомлення
        /// </summary>
        Task<bool> UpdateMessageAsync(MessageUpdateDto message);

        /// <summary>
        /// Видалити повідомлення
        /// </summary>
        Task<bool> DeleteMessageAsync(int messageId, int chatId);

        /// <summary>
        /// Повідомити про оновлення профілю
        /// </summary>
        Task<bool> NotifyProfileUpdateAsync(int userId, string newPfpUrl);

        /// <summary>
        /// Повідомити про оновлення групи
        /// </summary>
        Task<bool> NotifyGroupUpdateAsync(int chatId, string? name, string? description, string? iconUrl);

        /// <summary>
        /// Query user status from server
        /// </summary>
        Task<object?> QueryUserStatusAsync(int userId);

        // Call related APIs
        Task StartCallAsync(int chatId, string callUid, object? metadata = null);
    }
}