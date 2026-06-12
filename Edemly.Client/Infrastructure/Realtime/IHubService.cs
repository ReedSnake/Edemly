using Edemly.Contracts.Messages;
using Edemly.Contracts.Realtime;

namespace Edemly.Client.Infrastructure.Realtime
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

        event Action<IncomingCallEventDto>? IncomingCallReceived;

        event Action<int, int>? CallAcceptedReceived;

        event Action<CallAcceptedEventDto>? CallAcceptedDetailsReceived;

        event Action<int, int, string?>? CallRejectedReceived;

        event Action<int, int>? CallEndedReceived;

        event Action<CallParticipantUpdatedEventDto>? CallParticipantUpdatedReceived;

        event Action<CallingEventDto>? CallingReceived;

        event Action<GroupCallEventDto>? GroupCallUpdated;

        event Action<int, byte[], int, long, long>? AudioChunkReceived;

        bool IsConnected { get; }

        bool IsCallConnected { get; }

        Task<bool> ConnectAsync(string token);

        Task DisconnectAsync();

        Task<bool> SendMessageAsync(CreateMessageDto message);

        Task<bool> UpdateMessageAsync(UpdateMessageDto message);

        Task<bool> DeleteMessageAsync(int messageId, int chatId);

        Task<bool> NotifyProfileUpdateAsync(int userId, string newPfpUrl);

        Task<bool> NotifyGroupUpdateAsync(int chatId, string? name, string? description, string? iconUrl);

        Task<object?> QueryUserStatusAsync(int userId);

        Task StartCallAsync(int chatId, string callUid, object? metadata = null);

        Task AcceptCallAsync(int callId);

        Task RejectCallAsync(int callId, string? reason = null);

        Task EndCallAsync(int callId);

        Task SetCallMutedAsync(int callId, bool isMuted);

        Task SendAudioChunkAsync(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs);
    }
}
