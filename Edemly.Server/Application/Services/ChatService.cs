using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Contracts.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;

namespace Edemly.Server.Api.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatMemberService _chatMemberService;
        private readonly ILogger<ChatService> _logger;
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public ChatService(
            ServerDbContext serverDb,
            IChatMemberService chatMemberService,
            ILogger<ChatService> logger,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
        {
            _chatMemberService = chatMemberService;
            _logger = logger;
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        public async Task<(bool Success, string? Error, ChatDto? Chat)> CreateOrGetPrivateChat(int currentUserId, int otherUserId)
        {
            try
            {
                if (currentUserId == otherUserId)
                {
                    return (false, "Cannot create chat with yourself", null);
                }

                // Шукаємо існуючий приватний чат між двома користувачами
                var existingChat = await _ctx.Set<Chat>()
                    .Where(c => c.Type == ChatType.Direct)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == currentUserId))
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == otherUserId))
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                {
                    _logger.LogInformation($"Found existing private chat {existingChat.Id} between users {currentUserId} and {otherUserId}");

                    var dto = new ChatDto
                    {
                        Id = existingChat.Id,
                        Name = existingChat.Name,
                        Description = existingChat.Description,
                        IconUrl = existingChat.IconUrl,
                        Type = (int)existingChat.Type,
                        CreatedAt = existingChat.CreatedAt,
                        LastMessageTime = existingChat.LastMessageTime
                    };

                    return (true, null, dto);
                }

                // Створюємо новий приватний чат
                var otherUser = await _ctx.Set<User>().FindAsync(otherUserId);
                if (otherUser == null)
                {
                    return (false, "User not found", null);
                }

                var newChat = new Chat
                {
                    Name = $"Private chat",
                    Type = ChatType.Direct,
                    CreatedAt = DateTime.UtcNow
                };

                _ctx.Set<Chat>().Add(newChat);
                await _ctx.SaveChangesAsync();

                await _chatMemberService.AddMember(newChat.Id, currentUserId, ChatMemberRole.Base);
                await _chatMemberService.AddMember(newChat.Id, otherUserId, ChatMemberRole.Base);

                _logger.LogInformation($"Created new private chat {newChat.Id} between users {currentUserId} and {otherUserId}");

                var newDto = new ChatDto
                {
                    Id = newChat.Id,
                    Name = newChat.Name,
                    Description = newChat.Description,
                    IconUrl = newChat.IconUrl,
                    Type = (int)newChat.Type,
                    CreatedAt = newChat.CreatedAt,
                    LastMessageTime = newChat.LastMessageTime
                };

                return (true, null, newDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or getting private chat");
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // ✅ ДОДАНО: Метод для створення групового чату
        public async Task<(bool Success, string? Error, ChatDto? Chat)> CreateGroupChat(
            int creatorId,
            string groupName,
            List<int> participantIds)
        {
            try
            {
                // Перевірка назви групи
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return (false, "Group name cannot be empty", null);
                }

                _logger.LogInformation($"[CREATE GROUP] Creating group: '{groupName}' by user {creatorId}");

                // Перевірка учасників
                if (participantIds == null || participantIds.Count == 0)
                {
                    return (false, "Group must have at least one participant", null);
                }

                // Перевірка, що всі учасники існують
                var validUsers = await _ctx.Set<User>()
                    .Where(u => participantIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validUsers.Count != participantIds.Count)
                {
                    var invalidIds = participantIds.Except(validUsers);
                    return (false, $"Users not found: {string.Join(", ", invalidIds)}", null);
                }

                // ✅ ВИПРАВЛЕННЯ: Логуємо перед створенням
                _logger.LogInformation($"[CREATE GROUP] About to create Chat entity with Name='{groupName}', Type=Group");

                // Створюємо груповий чат
                var newChat = new Chat
                {
                    Name = groupName,  // ← ВАЖЛИВО: Зберігаємо назву
                    Type = ChatType.Group,  // ← ВАЖЛИВО: Встановлюємо тип Group
                    CreatedAt = DateTime.UtcNow,
                    LastMessageTime = DateTime.UtcNow  // ✅ ДОДАНО: Встановлюємо час останнього сообщення на час створення
                };

                _ctx.Set<Chat>().Add(newChat);
                await _ctx.SaveChangesAsync();

                // ✅ ВИПРАВЛЕННЯ: Детальне логування після збереження
                _logger.LogInformation($"[CREATE GROUP] Chat saved to DB: ID={newChat.Id}, Name='{newChat.Name}', Type={newChat.Type} (int={(int)newChat.Type}), LastMessageTime={newChat.LastMessageTime}");
                
                // ✅ ДОДАНО: Перевіряємо що реально збережено в БД
                var savedChat = await _ctx.Set<Chat>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == newChat.Id);
                if (savedChat != null)
                {
                    _logger.LogInformation($"[CREATE GROUP] Verified in DB: ID={savedChat.Id}, Name='{savedChat.Name}', Type={savedChat.Type} (int={(int)savedChat.Type}), LastMessageTime={savedChat.LastMessageTime}");
                }
                else
                {
                    _logger.LogError($"[CREATE GROUP] Chat {newChat.Id} not found in DB after save!");
                }

                // Додаємо створювача як адміністратора
                await _chatMemberService.AddMember(newChat.Id, creatorId, ChatMemberRole.Admin);

                // Додаємо учасників як звичайних членів
                foreach (var userId in participantIds)
                {
                    if (userId != creatorId) // Не додаємо створювача двічі
                    {
                        await _chatMemberService.AddMember(newChat.Id, userId, ChatMemberRole.Base);
                    }
                }

                _logger.LogInformation($"[CREATE GROUP] Successfully created group chat {newChat.Id} '{groupName}' with {participantIds.Count + 1} members");

                // ✅ ВИПРАВЛЕННЯ: Перевіряємо що повертаємо
                var dto = new ChatDto
                {
                    Id = newChat.Id,
                    Name = newChat.Name,
                    Description = newChat.Description,
                    IconUrl = newChat.IconUrl,
                    Type = (int)newChat.Type,  // ← ВАЖЛИВО: Повертаємо правильний тип
                    CreatedAt = newChat.CreatedAt,
                    LastMessageTime = newChat.LastMessageTime  // ✅ ВАЖЛИВО: Повертаємо LastMessageTime
                };

                _logger.LogInformation($"[CREATE GROUP] Returning DTO: ID={dto.Id}, Name='{dto.Name}', Type={dto.Type}, LastMessageTime={dto.LastMessageTime}");

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CREATE GROUP] Error creating group chat '{groupName}'");
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, List<ChatDto> Chats)> GetMyChats(int userId)
        {
            try
            {
                var chats = await _ctx.Set<Chat>()
                    .Include(c => c.ChatMembers)
                        .ThenInclude(cm => cm.User)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == userId))
                    .ToListAsync(); // ✅ ВИПРАВЛЕНО: Спочатку завантажуємо всі дані

                var result = new List<ChatDto>();

                foreach (var chat in chats)
                {
                    string displayName = chat.Name;

                    // ✅ ВИПРАВЛЕННЯ: Для приватних чатів показуємо ім'я співрозмовника
                    if (chat.Type == ChatType.Direct)
                    {
                        var otherMember = chat.ChatMembers.FirstOrDefault(m => m.UserId != userId);
                        if (otherMember?.User != null)
                        {
                            displayName = otherMember.User.Username;
                            _logger.LogInformation($"[GET MY CHATS] Private chat {chat.Id}: displaying as '{displayName}' for user {userId}");
                        }
                    }
                    // ✅ Для груп та каналів використовуємо назву з БД
                    else
                    {
                        _logger.LogInformation($"[GET MY CHATS] Group chat {chat.Id}: displaying as '{displayName}', Type={chat.Type}");
                    }

                    // Отримуємо останнє повідомлення
                    var lastMessage = await _ctx.Set<Message>()
                        .Where(m => m.ChatId == chat.Id)
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();

                    var dto = new ChatDto
                    {
                        Id = chat.Id,
                        Name = displayName,  // ← Правильне ім'я
                        Description = chat.Description,
                        IconUrl = chat.IconUrl,
                        Type = (int)chat.Type,  // ← Правильний тип з БД
                        CreatedAt = chat.CreatedAt,
                        LastMessageTime = chat.LastMessageTime,
                        LastMessageText = lastMessage?.Text,    
                        LastMessageSenderId = lastMessage?.SenderId
                    };

                    result.Add(dto);
                }

                // Сортуємо за останнім повідомленням або датою створення
                result = result
                    .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                    .ToList();

                _logger.LogInformation($"[GET MY CHATS] Returning {result.Count} chats for user {userId}");
                return (true, null, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chats");
                return (false, ex.Message, new List<ChatDto>());
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error, ChatDto? Chat)> GetById(int chatId)
        {
            try
            {
                var chat = await _ctx.Set<Chat>()
                    .Include(c => c.ChatMembers)
                    .FirstOrDefaultAsync(c => c.Id == chatId);
                    
                if (chat == null)
                {
                    return (false, "Chat not found", null);
                }

                var dto = new ChatDto
                {
                    Id = chat.Id,
                    Name = chat.Name,  // За замовчуванням використовуємо назву з БД (для груп)
                    Description = chat.Description,
                    IconUrl = chat.IconUrl,
                    Type = (int)chat.Type,
                    CreatedAt = chat.CreatedAt,
                    LastMessageTime = chat.LastMessageTime
                };

                _logger.LogInformation($"[GET BY ID] Returning chat {chatId} with name '{dto.Name}'");
                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat by id");
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        // ✅ ДОДАНО: Перегрузка для отримання чату з правильним іменем для конкретного користувача
        public async Task<(bool Success, string? Error, ChatDto? Chat)> GetById(int chatId, int requestingUserId)
        {
            try
            {
                var chat = await _ctx.Set<Chat>()
                    .Include(c => c.ChatMembers)
                        .ThenInclude(cm => cm.User)
                    .FirstOrDefaultAsync(c => c.Id == chatId);
                    
                if (chat == null)
                {
                    return (false, "Chat not found", null);
                }

                string displayName = chat.Name;

                // ✅ Для приватних чатів показуємо ім'я співрозмовника
                if (chat.Type == ChatType.Direct)
                {
                    var otherMember = chat.ChatMembers.FirstOrDefault(cm => cm.UserId != requestingUserId);
                    if (otherMember != null)
                    {
                        displayName = otherMember.User.Username;
                        _logger.LogInformation($"[GET BY ID] Private chat {chatId}: displaying as '{displayName}' for user {requestingUserId}");
                    }
                }
                // ✅ Для груп використовуємо назву групи
                else
                {
                    _logger.LogInformation($"[GET BY ID] Group chat {chatId}: displaying as '{displayName}'");
                }

                var dto = new ChatDto
                {
                    Id = chat.Id,
                    Name = displayName,
                    Description = chat.Description,
                    IconUrl = chat.IconUrl,
                    Type = (int)chat.Type,
                    CreatedAt = chat.CreatedAt,
                    LastMessageTime = chat.LastMessageTime
                };

                return (true, null, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting chat {chatId} for user {requestingUserId}");
                return (false, ex.Message, null);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }

        public async Task<(bool Success, string? Error)> UpdateChat(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                var chat = await _ctx.Set<Chat>().FindAsync(chatId);
                
                if (chat == null)
                {
                    return (false, "Chat not found");
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    chat.Name = name;
                }

                if (description != null)
                {
                    chat.Description = description;
                }

                if (iconUrl != null)
                {
                    chat.IconUrl = iconUrl;
                }

                await _ctx.SaveChangesAsync();
                
                _logger.LogInformation($"Updated chat {chatId}");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat");
                return (false, ex.Message);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
