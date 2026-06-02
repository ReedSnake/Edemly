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
            // First try injected tenant provider (works for HTTP controller scopes)
            Company? company = null;
            string source = "none";
            if (_tenantProvider != null && _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null)
            {
                company = _tenantProvider.CurrentCompany;
                source = "tenantProvider";
            }

            // Fallback: SignalR may create new scopes for hub methods, tenantProvider set during negotiation may not persist.
            // Check HttpContext.Items populated by TenantResolutionMiddleware during initial negotiate/connect request.
            if (company == null)
            {
                try
                {
                    var http = this.Context?.GetHttpContext();
                    if (http != null)
                    {
                        // 1) Try Items
                        if (http.Items.TryGetValue("TenantCompany", out var item) && item is Company c)
                        {
                            company = c;
                            source = "httpContext.Items";
                        }
                        else
                        {
                            // 2) Try query string 'tenant' (SignalR clients can send tenant as query param)
                            var tenantQuery = http.Request.Query["tenant"].FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(tenantQuery))
                            {
                                try
                                {
                                    var found = _serverDb.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Name == tenantQuery).GetAwaiter().GetResult();
                                    if (found != null)
                                    {
                                        company = found;
                                        source = "query-tenant";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "ResolveDbContext: error checking company by tenant query");
                                }
                            }

                            // 3) Additional fallback: try to extract first path segment as company name
                            if (company == null)
                            {
                                var path = http.Request?.Path.Value ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (segments.Length > 0)
                                    {
                                        var first = segments[0];
                                        // ignore common system segments
                                        if (!string.Equals(first, "api", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "main", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "hubs", StringComparison.OrdinalIgnoreCase) &&
                                            !string.Equals(first, "swagger", StringComparison.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                // Check if this segment matches a company in master DB
                                                var found = _serverDb.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Name == first).GetAwaiter().GetResult();
                                                if (found != null)
                                                {
                                                    company = found;
                                                    source = "path-first-segment";
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogDebug(ex, "ResolveDbContext: error checking company by path segment");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ResolveDbContext: error reading HttpContext.Items or Request.Path");
                }
            }

            if (company != null)
            {
                isTenant = true;
                _logger.LogDebug("ResolveDbContext: using tenant DB for company '{Company}' (source: {Source})", company.Name, source);
                return _tenantDbFactory.CreateCompanyDbContext(company);
            }

            isTenant = false;
            _logger.LogDebug("ResolveDbContext: using master DB (no tenant resolved)");
            return _serverDb;
        }

        public async Task SendMessage(CreateMessageDto messageDto)
        {
            var userId = GetUserId();

            var chatMemberUserIds = await GetChatMemberIds(messageDto.ChatId);

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

                var messageToSend = new MessageDto
                {
                    Id = msg.Id,
                    ChatId = msg.ChatId,
                    SenderId = msg.SenderId,
                    Text = msg.Text,
                    SentAt = msg.SentAt,
                    Type = (int)msg.Type,
                    ContentUrl = msg.ContentUrl,
                    FileName = msg.FileName
                };

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

        // helper wrapper to keep original behavior and simplify edits
        private async Task<bool> _message_service_Create(CreateMessageDto messageDto, int userId)
        {
            var result = await _messageService.Create(userId, messageDto);
            return result.Success;
        }

        public async Task UpdateMessage(UpdateMessageDto messageDto)
        {
            var userId = GetUserId();

            var validationResult = await ValidateMessageAccess(messageDto.ChatId, messageDto.Id, userId, requireSender: true);

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

                var updatedMessageDto = new MessageDto
                {
                    Id = message.Id,
                    ChatId = message.ChatId,
                    SenderId = message.SenderId,
                    Text = message.Text,
                    SentAt = message.SentAt,
                    Type = (int)message.Type,
                    ContentUrl = message.ContentUrl,
                    FileName = message.FileName
                };

                await Clients.Users(validationResult.ChatMemberIds).SendAsync("ReceiveMessageUpdate", updatedMessageDto);
            }
            finally
            {
                if (isTenant) ctx.Dispose();
            }
        }

        public async Task DeleteMessage(int messageId, int chatId)
        {
            var userId = GetUserId();

            var validationResult = await ValidateMessageDeletion(chatId, messageId, userId);

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

        private async Task<List<string>> GetChatMemberIds(int chatId)
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

        private async Task<(Message Message, List<string> ChatMemberIds)> ValidateMessageAccess(
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

        private async Task<(Message Message, ChatMember ChatMember, List<string> ChatMemberIds)> ValidateMessageDeletion(
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
        public async Task NotifyGroupCreated(int chatId, List<int> memberIds)
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
                throw new HubException($"Failed to notify group creation: {ex.Message}");
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
        public async Task NotifyProfileUpdated(int userId, string newPfpUrl)
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
        public async Task NotifyGroupUpdated(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                _logger.LogInformation("NotifyGroupUpdated called for chat {ChatId}", chatId);

                // Get all members of the chat
                var memberIds = await GetChatMemberIds(chatId);
                
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

        public async Task ConfirmRemindingReceived(int remindingId)
        {
            var userId = GetUserId();
            await _remindingService.ConfirmReminding(userId, remindingId);
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
