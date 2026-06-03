using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public class WelcomeChatService : IWelcomeChatService
    {
        private const string WelcomeChatName = "Edemly";
        private const string WelcomeChatDescription = "Official Edemly chat";
        private const string WelcomeChatIcon = "pack://application:,,,/Assets/logo.png";

        private readonly ILogger<WelcomeChatService> _logger;

        public WelcomeChatService(ILogger<WelcomeChatService> logger)
        {
            _logger = logger;
        }

        public async Task<(Chat Chat, bool Created)> EnsureWelcomeChatAsync(DbContext dbContext)
        {
            var chat = await dbContext.Set<Chat>()
                .FirstOrDefaultAsync(item => item.Name == WelcomeChatName && item.Type == ChatType.Group);

            if (chat != null)
            {
                return (chat, false);
            }

            chat = new Chat
            {
                Name = WelcomeChatName,
                Description = WelcomeChatDescription,
                IconUrl = WelcomeChatIcon,
                Type = ChatType.Group,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Set<Chat>().Add(chat);
            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Created welcome chat {ChatId}", chat.Id);
            return (chat, true);
        }

        public async Task EnsureUserInWelcomeChatAsync(DbContext dbContext, int userId)
        {
            var (chat, _) = await EnsureWelcomeChatAsync(dbContext);
            var exists = await dbContext.Set<ChatMember>()
                .AnyAsync(item => item.ChatId == chat.Id && item.UserId == userId);

            if (exists)
            {
                return;
            }

            dbContext.Set<ChatMember>().Add(new ChatMember
            {
                ChatId = chat.Id,
                UserId = userId,
                Role = ChatMemberRole.Base,
                JoinedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
            _logger.LogInformation("Added user {UserId} to welcome chat {ChatId}", userId, chat.Id);
        }
    }
}
