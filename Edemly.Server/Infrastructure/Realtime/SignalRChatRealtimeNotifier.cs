using Edemly.Contracts.Chats;
using Edemly.Server.Api.Hubs;
using Edemly.Server.Application.Chats;
using Microsoft.AspNetCore.SignalR;

namespace Edemly.Server.Infrastructure.Realtime
{
    public class SignalRChatRealtimeNotifier : IChatRealtimeNotifier
    {
        private readonly IHubContext<MainHub> _hubContext;
        private readonly ILogger<SignalRChatRealtimeNotifier> _logger;

        public SignalRChatRealtimeNotifier(
            IHubContext<MainHub> hubContext,
            ILogger<SignalRChatRealtimeNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyGroupCreatedAsync(ChatDto chat, int requesterId, IEnumerable<int> participantIds)
        {
            try
            {
                var memberIds = participantIds
                    .Append(requesterId)
                    .Distinct()
                    .Select(memberId => memberId.ToString())
                    .ToList();

                await _hubContext.Clients.Users(memberIds).SendAsync("GroupCreated", new
                {
                    ChatId = chat.Id,
                    ChatName = chat.Name,
                    ChatType = chat.Type,
                    CreatorId = requesterId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast group creation for chat {ChatId}", chat.Id);
            }
        }
    }
}