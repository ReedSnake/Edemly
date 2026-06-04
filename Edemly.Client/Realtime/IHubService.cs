namespace Edemly.Client.Realtime
{
    public interface IHubService : IDisposable
    {
        event Action<MessageDto>? MessageReceived;
        event Action<MessageDto>? MessageUpdated;
        event Action<int, int>? MessageDeleted;
        event Action<bool>? ConnectionStateChanged;
        event Action<int>? GroupCreated;
        event Action<int, string?, string?, string?>? GroupUpdated;
        event Action<int, bool, DateTime?>? UserStatusChanged;
        event Action<int, string>? ProfileUpdated;

        bool IsConnected { get; }

        Task<bool> ConnectAsync(string token);
        Task DisconnectAsync();
        Task<bool> SendMessageAsync(CreateMessageDto message);
        Task<bool> UpdateMessageAsync(UpdateMessageDto message);
        Task<bool> DeleteMessageAsync(int messageId, int chatId);
        Task<bool> NotifyProfileUpdateAsync(int userId, string newPfpUrl);
        Task<bool> NotifyGroupUpdateAsync(int chatId, string? name, string? description, string? iconUrl);
        Task<object?> QueryUserStatusAsync(int userId);
        Task StartCallAsync(int chatId, string callUid, object? metadata = null);
    }
}
