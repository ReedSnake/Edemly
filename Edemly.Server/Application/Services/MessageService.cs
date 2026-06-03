using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Utils;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Services;

namespace Edemly.Server.Api.Services
{
    public class MessageService : TenantAwareServiceBase, IMessageService
    {
        private readonly ILogger<MessageService> _logger;
        private readonly ChatCacheRegistry _registry;
        private readonly IMemoryCache _cache;

        public MessageService(
            ServerDbContext serverDb,
            ILogger<MessageService> logger,
            IMemoryCache cache,
            ChatCacheRegistry registry,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _logger = logger;
            _registry = registry;
            _cache = cache;
        }

        public async Task<ServiceDataResult<MessageDto>> GetById(int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(message => message.Id == id);

                if (msg == null)
                {
                    return ServiceDataResult<MessageDto>.NotFound("Message not found");
                }

                return ServiceDataResult<MessageDto>.Ok(ToDto(msg));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message by id");
                return ServiceDataResult<MessageDto>.Unexpected("Failed to get message");
            }
        }

        public async Task<ServiceDataResult<List<MessageDto>>> GetByChat(int currentUserId, int chatId, int page, int pageSize)
        {
            string cacheKey = ChatCacheRegistry.GetCacheKey(chatId, page, pageSize);

            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await IsInChatAsync(ctx, currentUserId, chatId))
                {
                    return ServiceDataResult<List<MessageDto>>.Forbidden();
                }

                if (_cache.TryGetValue(cacheKey, out List<MessageDto>? cached))
                {
                    return ServiceDataResult<List<MessageDto>>.Ok(cached ?? new List<MessageDto>());
                }

                var messages = await ctx.Set<Message>()
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.SentAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ChatId = m.ChatId,
                        SenderId = m.SenderId,
                        Text = m.Text,
                        Type = (int)m.Type,
                        SentAt = m.SentAt,
                        ContentUrl = m.ContentUrl,
                        FileName = m.FileName
                    })
                    .ToListAsync();

                _cache.Set(cacheKey, messages, TimeSpan.FromMinutes(5));
                _registry.RegisterKey(chatId, page, pageSize);

                return ServiceDataResult<List<MessageDto>>.Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages for chat");
                return ServiceDataResult<List<MessageDto>>.Unexpected("Failed to get messages");
            }
        }

        public async Task<ServiceDataResult<MessageDto>> GetLastByChat(int currentUserId, int chatId)
        {
            string cacheKey = ChatCacheRegistry.GetLastMessageCacheKey(chatId);

            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await IsInChatAsync(ctx, currentUserId, chatId))
                {
                    return ServiceDataResult<MessageDto>.Forbidden();
                }

                if (_cache.TryGetValue(cacheKey, out MessageDto? cached) && cached != null)
                {
                    return ServiceDataResult<MessageDto>.Ok(cached);
                }

                MessageDto? message = await ctx.Set<Message>()
                    .Where(m => m.ChatId == chatId)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ChatId = m.ChatId,
                        SenderId = m.SenderId,
                        Text = m.Text,
                        Type = (int)m.Type,
                        SentAt = m.SentAt,
                        ContentUrl = m.ContentUrl,
                        FileName = m.FileName
                    })
                    .FirstOrDefaultAsync();

                if (message != null)
                {
                    _cache.Set(cacheKey, message, TimeSpan.FromMinutes(5));
                    _registry.RegisterKey(chatId, 1, 1);
                    return ServiceDataResult<MessageDto>.Ok(message);
                }

                return ServiceDataResult<MessageDto>.Ok(new MessageDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get last message for chat");
                return ServiceDataResult<MessageDto>.Unexpected("Failed to get last message");
            }
        }

        public async Task<ServiceMessageResult> Create(int senderId, CreateMessageDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await IsInChatAsync(ctx, senderId, model.ChatId))
                {
                    return ServiceMessageResult.Forbidden();
                }

                var msg = new Message
                {
                    ChatId = model.ChatId,
                    SenderId = senderId,
                    Text = model.Text,
                    Type = (MessageType)model.Type,
                    ContentUrl = model.ContentUrl,
                    FileName = model.FileName,
                    SentAt = DateTime.UtcNow
                };

                ctx.Set<Message>().Add(msg);
                await ctx.SaveChangesAsync();

                ClearChatCache(msg.ChatId);
                return ServiceMessageResult.Ok("Message created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create message");
                return ServiceMessageResult.Unexpected("Failed to create message");
            }
        }

        public async Task<ServiceMessageResult> Update(int currentUserId, UpdateMessageDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>().FindAsync(model.Id);
                if (msg == null || msg.SenderId != currentUserId)
                {
                    return ServiceMessageResult.Forbidden();
                }

                if (!string.IsNullOrEmpty(model.Text))
                    msg.Text = model.Text;

                if (model.Type.HasValue)
                    msg.Type = (MessageType)model.Type.Value;

                if (model.ContentUrl != null)
                    msg.ContentUrl = model.ContentUrl;

                if (model.FileName != null)
                    msg.FileName = model.FileName;

                await ctx.SaveChangesAsync();
                ClearChatCache(msg.ChatId);

                return ServiceMessageResult.Ok("Message updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update message");
                return ServiceMessageResult.Unexpected("Failed to update message");
            }
        }

        public async Task<ServiceMessageResult> Delete(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var msg = await ctx.Set<Message>().FindAsync(id);
                if (msg == null)
                {
                    return ServiceMessageResult.Forbidden();
                }

                if (!await CanDeleteMessageAsync(ctx, currentUserId, msg))
                {
                    return ServiceMessageResult.Forbidden();
                }

                ctx.Set<Message>().Remove(msg);
                await ctx.SaveChangesAsync();

                ClearChatCache(msg.ChatId);
                return ServiceMessageResult.Ok("Message deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message");
                return ServiceMessageResult.Unexpected("Failed to delete message");
            }
        }

        private static Task<bool> IsInChatAsync(DbContext ctx, int userId, int chatId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
        }

        private static async Task<bool> CanDeleteMessageAsync(DbContext ctx, int currentUserId, Message message)
        {
            if (message.SenderId == currentUserId)
            {
                return true;
            }

            var currentMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == message.ChatId);

            if (currentMember == null)
            {
                return false;
            }

            return currentMember.Role == ChatMemberRole.Admin || currentMember.Role == ChatMemberRole.Creator;
        }

        private void ClearChatCache(int chatId)
        {
            _registry.ClearChat(chatId, _cache);
        }

        private static MessageDto ToDto(Message msg)
        {
            return new MessageDto
            {
                Id = msg.Id,
                ChatId = msg.ChatId,
                SenderId = msg.SenderId,
                Text = msg.Text,
                Type = (int)msg.Type,
                SentAt = msg.SentAt,
                ContentUrl = msg.ContentUrl,
                FileName = msg.FileName
            };
        }
    }
}
