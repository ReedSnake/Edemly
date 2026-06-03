using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Server.Api.Middleware; // ITenantProvider

namespace Edemly.Server.Hubs
{
    [Authorize]
    public class MainHub : Hub
    {
        private readonly IMessageService _messageService;
        private readonly IRemindingService _remindingService;
        private readonly ServerDbContext _serverDb;
        private readonly Services.UserPresenceService _presenceService;
        private readonly ILogger<MainHub> _logger;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbFactory;
        private readonly IMemoryCache _cache;
        private readonly ChatCacheRegistry _cacheRegistry;

        public MainHub(IRemindingService remindingService, IMessageService messageService, ServerDbContext serverDb, Services.UserPresenceService presenceService, ILogger<MainHub> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IMemoryCache cache, ChatCacheRegistry cacheRegistry)
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

            var chatMemberUserIds = await GetChatMemberIdsAsync(messageDto.ChatId);

            if (!chatMemberUserIds.Contains(userId.ToString()))
            {
                throw new HubException("User is not a member of this chat");
            }

            // Create message directly in the determined DB (tenant or master)
            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                // Ensure chat exists. If chat not found, log warning and throw.
                var chat = await ctx.Set<Chat>().FirstOrDefaultAsync(c => c.Id == messageDto.ChatId);
                if (chat == null)
                {
                    _logger.LogWarning("SendMessage: Chat {ChatId} not found. Creating placeholder chat.", messageDto.ChatId);

                    // Create a placeholder group chat so message can be stored and clients will receive chat overview later.
                    // Note: This will create a new chat with a new Id; we then associate message with that chat.
                    var placeholder = new Chat
                    {
                        Name = $"Chat {messageDto.ChatId}",
                        Type = ChatType.Group,
                        CreatedAt = DateTime.UtcNow
                    };

                    ctx.Set<Chat>().Add(placeholder);
                    await ctx.SaveChangesAsync();

                    chat = placeholder;
                    // Update member list: do not auto-add members here as we don't know them in hub context.
                    _logger.LogInformation("Placeholder chat created with Id {NewChatId} for missing chat {OldChatRef}", chat.Id, messageDto.ChatId);
                }

                var msg = new Message
                {
                    ChatId = chat.Id,
                    SenderId = userId,
                    Text = messageDto.Text,
                    SentAt = DateTime.UtcNow,
                    Type = (MessageType)messageDto.Type,
                    ContentUrl = messageDto.ContentUrl,
                    FileName = messageDto.FileName
                };

                ctx.Set<Message>().Add(msg);
                await ctx.SaveChangesAsync();
                _cacheRegistry.ClearChat(msg.ChatId, _cache);

                // Update chat last message time so clients will show updated chat ordering and preview
                try
                {
                    chat.LastMessageTime = msg.SentAt;
                    ctx.Set<Chat>().Update(chat);
                    await ctx.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update LastMessageTime for chat {ChatId}", chat.Id);
                }

                var messageToSend = MessageMappings.ToDto(msg);

                // If we created a placeholder chat, some member ids may be invalid. Recompute recipients from actual chat id.
                var recipients = chatMemberUserIds;
                if (chat.Id != messageDto.ChatId)
                {
                    try
                    {
                        recipients = await ctx.Set<ChatMember>().Where(cm => cm.ChatId == chat.Id).Select(cm => cm.UserId.ToString()).ToListAsync();
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

            var validationResult = await ValidateMessageAccessAsync(messageDto.ChatId, messageDto.Id, userId, requireSender: true);

            // Update message directly in resolved DB
            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var message = await ctx.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageDto.Id && m.ChatId == messageDto.ChatId);
                if (message == null)
                {
                    throw new HubException("Message not found");
                }

                if (!string.IsNullOrEmpty(messageDto.Text)) message.Text = messageDto.Text;
                if (messageDto.Type.HasValue) message.Type = (MessageType)messageDto.Type.Value;
                if (messageDto.ContentUrl != null) message.ContentUrl = messageDto.ContentUrl;
                if (messageDto.FileName != null) message.FileName = messageDto.FileName;

                ctx.Update(message);
                await ctx.SaveChangesAsync();
                _cacheRegistry.ClearChat(message.ChatId, _cache);

                var updatedMessageDto = MessageMappings.ToDto(message);

                await Clients.Users(validationResult.ChatMemberIds).SendAsync("ReceiveMessageUpdate", updatedMessageDto);
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

            var validationResult = await ValidateMessageDeletionAsync(chatId, messageId, userId);

            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var message = await ctx.Set<Message>().FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);
                if (message == null)
                {
                    throw new HubException("Message not found");
                }

                ctx.Set<Message>().Remove(message);
                await ctx.SaveChangesAsync();
                _cacheRegistry.ClearChat(chatId, _cache);

                // Send two separate typed arguments so clients using typed handlers receive them correctly
                await Clients.Users(validationResult.ChatMemberIds).SendAsync("ReceiveMessageDelete", messageId, chatId);
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
                return await ctx.Set<ChatMember>()
                    .Where(cm => cm.ChatId == chatId)
                    .Select(cm => cm.UserId.ToString())
                    .ToListAsync();
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        private async Task<(Message Message, List<string> ChatMemberIds)> ValidateMessageAccessAsync(
            int chatId,
            int messageId,
            int userId,
            bool requireSender = false)
        {
            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var query = from Message in ctx.Set<Message>()
                            where Message.Id == messageId && Message.ChatId == chatId
                            join cm in ctx.Set<ChatMember>() on chatId equals cm.ChatId
                            select new { Message, cm };

                var results = await query.ToListAsync();

                if (!results.Any())
                {
                    throw new HubException("Message not found");
                }

                var message = results.First().Message;
                var chatMemberIds = results.Select(r => r.cm.UserId.ToString()).Distinct().ToList();

                if (!chatMemberIds.Contains(userId.ToString()))
                {
                    throw new HubException("User is not a member of this chat");
                }

                if (requireSender && message.SenderId != userId)
                {
                    throw new HubException("You can only update your own messages");
                }

                return (message, chatMemberIds);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        private async Task<(Message Message, ChatMember ChatMember, List<string> ChatMemberIds)> ValidateMessageDeletionAsync(
            int chatId,
            int messageId,
            int userId)
        {
            DbContext ctx = ResolveDbContext(out var isTenant);
            try
            {
                var query = from Message in ctx.Set<Message>()
                            where Message.Id == messageId && Message.ChatId == chatId
                            from cm in ctx.Set<ChatMember>()
                            where cm.ChatId == chatId
                            select new { Message, cm };

                var results = await query.ToListAsync();

                if (!results.Any())
                {
                    throw new HubException("Message not found");
                }

                var message = results.First().Message;
                var userChatMember = results.FirstOrDefault(r => r.cm.UserId == userId)?.cm;

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

                var chatMemberIds = results.Select(r => r.cm.UserId.ToString()).Distinct().ToList();

                return (message, userChatMember, chatMemberIds);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        /// <summary>
        /// Notification when group created
        /// </summary>
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

        /// <summary>
        /// Query user status
        /// </summary>
        public Models.UserOnlineStatus? GetUserStatus(int userId)
        {
            return _presenceService.GetUserStatus(userId);
        }

        /// <summary>
        /// Notify profile update to all clients
        /// </summary>
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

        /// <summary>
        /// Notify group update to all group members
        /// </summary>
        [HubMethodName("NotifyGroupUpdated")]
        public async Task NotifyGroupUpdatedAsync(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                _logger.LogInformation("NotifyGroupUpdated called for chat {ChatId}", chatId);

                // Get all members of the chat
                var memberIds = await GetChatMemberIdsAsync(chatId);
                
                if (memberIds == null || !memberIds.Any())
                {
                    _logger.LogWarning("No members found for chat {ChatId}", chatId);
                    return;
                }

                var payload = new { chatId = chatId, name = name, description = description, iconUrl = iconUrl };
                _logger.LogDebug("Broadcasting GroupUpdated to {Count} members: {Payload}", memberIds.Count, System.Text.Json.JsonSerializer.Serialize(payload));

                // Send to all group members (including the sender)
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
