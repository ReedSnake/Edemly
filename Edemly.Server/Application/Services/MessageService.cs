using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Utils;
using Edemly.Server.Api.Middleware; // for ITenantProvider
using Edemly.Server.Services; // for ITenantDbContextFactory

namespace Edemly.Server.Api.Services
{
    public class MessageService : IMessageService
    {
        private readonly ILogger<MessageService> _logger;
        private readonly ChatCacheRegistry _registry;
        private readonly IMemoryCache _cache;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public MessageService(ServerDbContext serverDb, ILogger<MessageService> logger, IMemoryCache cache, ChatCacheRegistry registry, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _logger = logger;
            _registry = registry;
            _cache = cache;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }


        // Get a single message by Id
        public async Task<(bool Success, string? Error, MessageDto Message)> GetById(int id)
        {
            try
            {
                var msg = await _ctx.Set<Message>().FindAsync(id);
                if (msg == null)
                    return (false, "Message not found", null!);

                var dto = new MessageDto
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

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get message by id");
                return (false, ex.Message, null!);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<MessageDto> Messages)> GetByChat(int chatId, int page, int pageSize)
        {
            string cacheKey = ChatCacheRegistry.GetCacheKey(chatId, page, pageSize);

            if (_cache.TryGetValue(cacheKey, out List<MessageDto>? cached))
                return (true, null, cached ?? new List<MessageDto>());

            try
            {
                var messages = await _ctx.Set<Message>()
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

                return (true, null, messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get messages for chat");
                return (false, ex.Message, new List<MessageDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        //Get last message in chat 
        public async Task<(bool Success, string? Error, MessageDto Message)> GetLastByChat(int chatId)
        {
            string cacheKey = ChatCacheRegistry.GetLastMessageCacheKey(chatId);

            if (_cache.TryGetValue(cacheKey, out MessageDto? cached) && cached != null)
                return (true, null, cached);

            try
            {
                MessageDto? message = await _ctx.Set<Message>()
                    .Where(m => m.ChatId == chatId)
                    .OrderByDescending(m => m.SentAt)
                    .Take(1)
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
                    }).FirstOrDefaultAsync();

                if (message != null)
                {
                    _cache.Set(cacheKey, message, TimeSpan.FromMinutes(5));
                    _registry.RegisterKey(chatId, 1, 1);
                }

                return (true, null, message ?? new MessageDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get last message for chat");
                return (false, ex.Message, new MessageDto());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        // Create a new message
        public async Task<(bool Success, string? Error)> Create(int senderId, CreateMessageDto model)
        {
            try
            {
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

                _ctx.Set<Message>().Add(msg);
                await _ctx.SaveChangesAsync();

                _registry.ClearChat(msg.ChatId, _cache);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create message");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Update an existing message
        public async Task<(bool Success, string? Error)> Update(UpdateMessageDto model)
        {
            try
            {
                var msg = await _ctx.Set<Message>().FindAsync(model.Id);
                if (msg == null)
                    return (false, "Message not found");

                if (!string.IsNullOrEmpty(model.Text))
                    msg.Text = model.Text;

                if (model.Type.HasValue)
                    msg.Type = (MessageType)model.Type.Value;

                if (model.ContentUrl != null)
                    msg.ContentUrl = model.ContentUrl;

                await _ctx.SaveChangesAsync();

                _registry.ClearChat(msg.ChatId, _cache);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update message");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // Delete a message
        public async Task<(bool Success, string? Error)> Delete(int id)
        {
            try
            {
                var msg = await _ctx.Set<Message>().FindAsync(id);
                if (msg == null)
                    return (false, "Message not found");

                _ctx.Set<Message>().Remove(msg);
                await _ctx.SaveChangesAsync();

                _registry.ClearChat(msg.ChatId, _cache);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete message");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
