namespace Edemly.Server.Api.Services
{
    public interface IChatRealtimeNotifier
    {
        Task NotifyGroupCreatedAsync(ChatDto chat, int requesterId, IEnumerable<int> participantIds);
    }
}