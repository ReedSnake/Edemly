using Edemly.Contracts.Chats;

namespace Edemly.Server.Application.Chats
{
    public interface IChatRealtimeNotifier
    {
        Task NotifyGroupCreatedAsync(ChatDto chat, int requesterId, IEnumerable<int> participantIds);
    }
}