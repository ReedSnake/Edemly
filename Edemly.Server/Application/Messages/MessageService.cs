using Edemly.Contracts.Messages;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Caching;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Edemly.Server.Application.Messages
{
    public class MessageService : TenantAwareServiceBase, IMessageService
    {
        private static readonly TimeSpan MessageCacheDuration = TimeSpan.FromMinutes(5);

        private readonly ILogger<MessageService> _logger;
        private readonly ChatCacheRegistry _chatCacheRegistry;
        private readonly IMemoryCache _memoryCache;

        public MessageService(
            ServerDbContext serverDbContext,
            ILogger<MessageService> logger,
            IMemoryCache memoryCache,
            ChatCacheRegistry chatCacheRegistry,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
            _chatCacheRegistry = chatCacheRegistry;
            _memoryCache = memoryCache;
        }

        public async Task<ServiceResult<MessageDto>> GetByIdAsync(int messageId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>()
                    .AsNoTracking()
                    .Where(message => message.Id == messageId)
                    .Select(MessageMappings.Projection)
                    .FirstOrDefaultAsync();

                if (msg == null)
                {
                    return ServiceResult<MessageDto>.NotFound("Message not found");
                }

                return ServiceResult<MessageDto>.Ok(msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message by id {MessageId}", messageId);
                return ServiceResult<MessageDto>.Unexpected("Failed to get message");
            }
        }

        public async Task<ServiceResult<List<MessageDto>>> GetByChatAsync(int currentUserId, int chatId, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            string cacheKey = ChatCacheRegistry.GetCacheKey(chatId, page, pageSize);

            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var accessResult = await ValidateChatAccessAsync(ctx, currentUserId, chatId);
                if (accessResult != null)
                {
                    return ToDataFailure<List<MessageDto>>(accessResult);
                }

                if (_memoryCache.TryGetValue(cacheKey, out List<MessageDto>? cached))
                {
                    return ServiceResult<List<MessageDto>>.Ok(cached ?? new List<MessageDto>());
                }

                var messages = await ctx.Set<Message>()
                    .AsNoTracking()
                    .Where(m => m.ChatId == chatId)
                    .OrderByDescending(m => m.SentAt)
                    .ThenByDescending(m => m.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(MessageMappings.Projection)
                    .ToListAsync();

                messages.Reverse();

                _memoryCache.Set(cacheKey, messages, MessageCacheDuration);
                _chatCacheRegistry.RegisterKey(chatId, page, pageSize);

                return ServiceResult<List<MessageDto>>.Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages for chat");
                return ServiceResult<List<MessageDto>>.Unexpected("Failed to get messages");
            }
        }

        public async Task<ServiceResult<MessageDto>> GetLastByChatAsync(int currentUserId, int chatId)
        {
            string cacheKey = ChatCacheRegistry.GetLastMessageCacheKey(chatId);

            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var accessResult = await ValidateChatAccessAsync(ctx, currentUserId, chatId);
                if (accessResult != null)
                {
                    return ToDataFailure<MessageDto>(accessResult);
                }

                if (_memoryCache.TryGetValue(cacheKey, out MessageDto? cached) && cached != null)
                {
                    return ServiceResult<MessageDto>.Ok(cached);
                }

                MessageDto? message = await ctx.Set<Message>()
                    .AsNoTracking()
                    .Where(m => m.ChatId == chatId)
                    .OrderByDescending(m => m.SentAt)
                    .ThenByDescending(m => m.Id)
                    .Select(MessageMappings.Projection)
                    .FirstOrDefaultAsync();

                if (message != null)
                {
                    _memoryCache.Set(cacheKey, message, MessageCacheDuration);
                    return ServiceResult<MessageDto>.Ok(message);
                }

                return ServiceResult<MessageDto>.Ok(new MessageDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get last message for chat");
                return ServiceResult<MessageDto>.Unexpected("Failed to get last message");
            }
        }

        public async Task<ServiceResult> CreateAsync(int currentUserId, CreateMessageDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var accessResult = await ValidateChatAccessAsync(ctx, currentUserId, request.ChatId);
                if (accessResult != null)
                {
                    return accessResult;
                }

                var msg = new Message
                {
                    ChatId = request.ChatId,
                    SenderId = currentUserId,
                    Text = request.Text,
                    Type = (MessageType)request.Type,
                    ContentUrl = request.ContentUrl,
                    FileName = request.FileName,
                    SentAt = DateTime.UtcNow
                };

                ctx.Set<Message>().Add(msg);
                await ctx.SaveChangesAsync();

                ClearChatCache(msg.ChatId);
                return ServiceResult.Ok("Message created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create message");
                return ServiceResult.Unexpected("Failed to create message");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int currentUserId, UpdateMessageDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>().FindAsync(request.Id);
                if (msg == null)
                {
                    return ServiceResult.NotFound("Message not found");
                }

                if (msg.SenderId != currentUserId)
                {
                    return ServiceResult.Forbidden();
                }

                if (!string.IsNullOrEmpty(request.Text))
                    msg.Text = request.Text;

                if (request.Type.HasValue)
                    msg.Type = (MessageType)request.Type.Value;

                if (request.ContentUrl != null)
                    msg.ContentUrl = request.ContentUrl;

                if (request.FileName != null)
                    msg.FileName = request.FileName;

                await ctx.SaveChangesAsync();
                ClearChatCache(msg.ChatId);

                return ServiceResult.Ok("Message updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update message");
                return ServiceResult.Unexpected("Failed to update message");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int requesterId, int messageId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>().FindAsync(messageId);
                if (msg == null)
                {
                    return ServiceResult.NotFound("Message not found");
                }

                if (!await CanDeleteMessageAsync(ctx, requesterId, msg))
                {
                    return ServiceResult.Forbidden();
                }

                ctx.Set<Message>().Remove(msg);
                await ctx.SaveChangesAsync();

                ClearChatCache(msg.ChatId);
                return ServiceResult.Ok("Message deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message");
                return ServiceResult.Unexpected("Failed to delete message");
            }
        }

        private static async Task<bool> CanDeleteMessageAsync(DbContext ctx, int requesterId, Message message)
        {
            if (message.SenderId == requesterId)
            {
                return true;
            }

            var requesterRole = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(chatMember => chatMember.UserId == requesterId && chatMember.ChatId == message.ChatId)
                .Select(chatMember => (ChatMemberRole?)chatMember.Role)
                .FirstOrDefaultAsync();

            return requesterRole == ChatMemberRole.Admin || requesterRole == ChatMemberRole.Creator;
        }

        private static async Task<ServiceResult?> ValidateChatAccessAsync(DbContext ctx, int currentUserId, int chatId)
        {
            var isMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(chatMember => chatMember.UserId == currentUserId && chatMember.ChatId == chatId);

            if (isMember)
            {
                return null;
            }

            var chatExists = await ctx.Set<Chat>()
                .AsNoTracking()
                .AnyAsync(chat => chat.Id == chatId);

            if (!chatExists)
            {
                return ServiceResult.NotFound("Chat not found");
            }

            return ServiceResult.Forbidden();
        }

        private static ServiceResult<T> ToDataFailure<T>(ServiceResult result)
        {
            return new ServiceResult<T>(false, result.StatusCode, default, result.Message);
        }

        private void ClearChatCache(int chatId)
        {
            _chatCacheRegistry.ClearChat(chatId, _memoryCache);
        }
    }
}
