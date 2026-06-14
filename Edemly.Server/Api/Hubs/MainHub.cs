using Edemly.Contracts.Messages;
using Edemly.Server.Api.Middleware; // ITenantProvider
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Application.Messages;
using Edemly.Server.Application.Remindings;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Caching;
using Edemly.Server.Infrastructure.Presence;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Edemly.Server.Api.Hubs
{
    [Authorize]
    public class MainHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IRemindingService _remindingService;
        private readonly ServerDbContext _serverDb;
        private readonly UserPresenceService _presenceService;
        private readonly ILogger<MainHub> _logger;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbFactory;
        private readonly IMemoryCache _cache;
        private readonly ChatCacheRegistry _cacheRegistry;

        public MainHub(IRemindingService remindingService, IMessageService messageService, ServerDbContext serverDb, UserPresenceService presenceService, ILogger<MainHub> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IMemoryCache cache, ChatCacheRegistry cacheRegistry)
        {
            _messageService = messageService;
            _remindingService = remindingService;
            _serverDb = serverDb;
            _presenceService = presenceService;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _tenantDbFactory = tenantDbFactory;
            _cache = cache;
            _cacheRegistry = cacheRegistry;
        }

        private DbContext ResolveDbContext(out bool isTenant)
        {
            var company = TenantRequestContext.GetCurrentCompany(Context?.GetHttpContext(), _tenantProvider);

            if (company != null)
            {
                isTenant = true;
                _logger.LogDebug("ResolveDbContext: using tenant DB for company '{Company}'", company.Name);
                return _tenantDbFactory.CreateCompanyDbContext(company);
            }

            isTenant = false;
            _logger.LogDebug("ResolveDbContext: using master DB (no tenant resolved)");
            return _serverDb;
        }

        [HubMethodName("SendMessage")]
        public async Task SendMessageAsync(CreateMessageDto messageDto)
        {
            var userId = GetUserId();

            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var chatMemberUserIds = await GetChatMemberUserIdsAsync(ctx, messageDto.ChatId);

                if (!chatMemberUserIds.Contains(userId))
                {
                    throw new HubException("User is not a member of this chat");
                }

                var chat = await ctx.Set<Chat>().FirstOrDefaultAsync(c => c.Id == messageDto.ChatId);
                var sentAt = DateTime.UtcNow;

                if (chat == null)
                {
                    _logger.LogWarning("SendMessage: Chat {ChatId} not found. Creating placeholder chat.", messageDto.ChatId);

                    var placeholder = new Chat
                    {
                        Name = $"Chat {messageDto.ChatId}",
                        Type = ChatType.Group,
                        CreatedAt = sentAt,
                        LastMessageTime = sentAt
                    };

                    ctx.Set<Chat>().Add(placeholder);
                    await ctx.SaveChangesAsync();

                    chat = placeholder;
                    _logger.LogInformation("Placeholder chat created with Id {NewChatId} for missing chat {OldChatRef}", chat.Id, messageDto.ChatId);
                }

                var msg = new Message
                {
                    ChatId = chat.Id,
                    SenderId = userId,
                    Text = messageDto.Text,
                    SentAt = sentAt,
                    Type = (MessageType)messageDto.Type,
                    ContentUrl = messageDto.ContentUrl,
                    FileName = messageDto.FileName
                };

                ctx.Set<Message>().Add(msg);
                await ctx.SaveChangesAsync();

                ChatLastMessageSnapshot.Apply(chat, msg);
                await ctx.SaveChangesAsync();

                _cacheRegistry.ClearChat(msg.ChatId, _cache);

                var messageToSend = MessageMappings.ToDto(msg);

                var recipients = ToSignalRUserIds(chatMemberUserIds);
                if (chat.Id != messageDto.ChatId)
                {
                    try
                    {
                        recipients = ToSignalRUserIds(await GetChatMemberUserIdsAsync(ctx, chat.Id));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "SendMessage: failed to recompute recipients for placeholder chat {ChatId}", chat.Id);
                    }
                }

                await Clients.Users(recipients).SendAsync("ReceiveMessage", messageToSend);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        [HubMethodName("UpdateMessage")]
        public async Task UpdateMessageAsync(UpdateMessageDto messageDto)
        {
            var userId = GetUserId();

            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var message = await ctx.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageDto.Id && m.ChatId == messageDto.ChatId);
                if (message == null)
                {
                    throw new HubException("Message not found");
                }

                var chatMemberUserIds = await GetChatMemberUserIdsAsync(ctx, messageDto.ChatId);
                if (!chatMemberUserIds.Contains(userId))
                {
                    throw new HubException("User is not a member of this chat");
                }

                if (message.SenderId != userId)
                {
                    throw new HubException("You can only update your own messages");
                }

                if (!string.IsNullOrEmpty(messageDto.Text)) message.Text = messageDto.Text;
                if (messageDto.Type.HasValue) message.Type = (MessageType)messageDto.Type.Value;
                if (messageDto.ContentUrl != null) message.ContentUrl = messageDto.ContentUrl;
                if (messageDto.FileName != null) message.FileName = messageDto.FileName;

                await ChatLastMessageSnapshot.ApplyIfCurrentAsync(ctx, message);

                ctx.Update(message);
                await ctx.SaveChangesAsync();
                _cacheRegistry.ClearChat(message.ChatId, _cache);

                var updatedMessageDto = MessageMappings.ToDto(message);

                await Clients.Users(ToSignalRUserIds(chatMemberUserIds)).SendAsync("ReceiveMessageUpdate", updatedMessageDto);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        [HubMethodName("DeleteMessage")]
        public async Task DeleteMessageAsync(int messageId, int chatId)
        {
            var userId = GetUserId();

            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var message = await ctx.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);
                if (message == null)
                {
                    throw new HubException("Message not found");
                }

                var chatMembers = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .Where(cm => cm.ChatId == chatId)
                    .Select(cm => new { cm.UserId, cm.Role })
                    .ToListAsync();

                var userChatMember = chatMembers.FirstOrDefault(cm => cm.UserId == userId);
                if (userChatMember == null)
                {
                    throw new HubException("User is not a member of this chat");
                }

                bool isAdmin = userChatMember.Role == ChatMemberRole.Admin || userChatMember.Role == ChatMemberRole.Creator;
                bool isSender = message.SenderId == userId;

                if (!isAdmin && !isSender)
                {
                    throw new HubException("You don't have permission to delete this message");
                }

                await ChatLastMessageSnapshot.RefreshAfterDeletingAsync(ctx, message);

                ctx.Set<Message>().Remove(message);
                await ctx.SaveChangesAsync();
                _cacheRegistry.ClearChat(chatId, _cache);

                await Clients.Users(ToSignalRUserIds(chatMembers.Select(cm => cm.UserId))).SendAsync("ReceiveMessageDelete", messageId, chatId);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        private int GetUserId()
        {
            var userIdClaim = Context.User?.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new HubException("User not authenticated");
            return int.Parse(userIdClaim);
        }

        private async Task<List<string>> GetChatMemberIdsAsync(int chatId)
        {
            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                return ToSignalRUserIds(await GetChatMemberUserIdsAsync(ctx, chatId));
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        private static Task<List<int>> GetChatMemberUserIdsAsync(DbContext ctx, int chatId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(cm => cm.ChatId == chatId)
                .Select(cm => cm.UserId)
                .ToListAsync();
        }

        private static List<string> ToSignalRUserIds(IEnumerable<int> userIds)
        {
            return userIds
                .Select(userId => userId.ToString())
                .Distinct()
                .ToList();
        }

        [HubMethodName("NotifyGroupCreated")]
        public async Task NotifyGroupCreatedAsync(int chatId, List<int> memberIds)
        {
            try
            {
                var memberIdStrings = memberIds.Select(id => id.ToString()).ToList();

                _logger.LogInformation("NotifyGroupCreated: chatId={ChatId}, members={Count}", chatId, memberIdStrings.Count);

                await Clients.Users(memberIdStrings).SendAsync("GroupCreated", new { ChatId = chatId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify group creation for chat {ChatId}", chatId);
                throw new HubException("Failed to notify group creation");
            }
        }

        public UserOnlineStatus? GetUserStatus(int userId)
        {
            return _presenceService.GetUserStatus(userId);
        }

        [HubMethodName("NotifyProfileUpdated")]
        public async Task NotifyProfileUpdatedAsync(int userId, string newPfpUrl)
        {
            try
            {
                _logger.LogInformation("NotifyProfileUpdated called for user {UserId}", userId);

                var payload = new { userId = userId, pfpUrl = newPfpUrl };
                _logger.LogDebug("Broadcasting ProfileUpdated: {Payload}", System.Text.Json.JsonSerializer.Serialize(payload));

                await Clients.Others.SendAsync("ProfileUpdated", payload);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Error: {e}");
            }
        }

        [HubMethodName("NotifyGroupUpdated")]
        public async Task NotifyGroupUpdatedAsync(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                _logger.LogInformation("NotifyGroupUpdated called for chat {ChatId}", chatId);

                var memberIds = await GetChatMemberIdsAsync(chatId);

                if (memberIds == null || !memberIds.Any())
                {
                    _logger.LogWarning("No members found for chat {ChatId}", chatId);
                    return;
                }

                var payload = new { chatId = chatId, name = name, description = description, iconUrl = iconUrl };
                _logger.LogDebug("Broadcasting GroupUpdated to {Count} members: {Payload}", memberIds.Count, System.Text.Json.JsonSerializer.Serialize(payload));

                await Clients.Users(memberIds).SendAsync("GroupUpdated", payload);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error notifying group update for chat {ChatId}", chatId);
            }
        }

        [HubMethodName("ConfirmRemindingReceived")]
        public async Task ConfirmRemindingReceivedAsync(int remindingId)
        {
            var userId = GetUserId();
            await _remindingService.ConfirmRemindingAsync(userId, remindingId);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            var connectionId = Context.ConnectionId;

            _logger.LogInformation("OnConnected: user={UserId} connection={ConnectionId}", userId, connectionId);

            _presenceService.SetUserOnline(userId, connectionId);

            var status = _presenceService.GetUserStatus(userId);
            _logger.LogInformation("After SetUserOnline: user={UserId} isOnline={IsOnline} connections={ConnCount}", userId, status?.IsOnline, status == null ? 0 : (_presenceService.GetOnlineUsers().Count));

            var payload = new { userId = userId, isOnline = true };
            _logger.LogDebug("Sending UserStatusChanged (online) payload: {Payload}", System.Text.Json.JsonSerializer.Serialize(payload));
            await Clients.Others.SendAsync("UserStatusChanged", payload);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            var (stillOnline, uid) = _presenceService.SetUserOffline(Context.ConnectionId);
            if (!stillOnline && uid.HasValue)
                await Clients.Others.SendAsync("UserStatusChanged", new { userId = uid.Value, isOnline = false, lastSeen = DateTime.UtcNow });
            await base.OnDisconnectedAsync(exception);
        }
    }
}
