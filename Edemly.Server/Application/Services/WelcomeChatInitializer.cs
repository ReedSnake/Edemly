using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Services
{
    public class WelcomeChatInitializer
    {
        private readonly ServerDbContext _context;
        private readonly ILogger<WelcomeChatInitializer> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWelcomeChatService _welcomeChatService;

        public WelcomeChatInitializer(
            ServerDbContext context,
            ILogger<WelcomeChatInitializer> logger,
            IConfiguration configuration,
            IWelcomeChatService welcomeChatService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _welcomeChatService = welcomeChatService;
        }

        public async Task InitializeWelcomeChatAsync()
        {
            try
            {
                _logger.LogInformation("Checking for welcome chat...");

                var (welcomeChat, created) = await _welcomeChatService.EnsureWelcomeChatAsync(_context);
                if (!created)
                {
                    _logger.LogInformation("Welcome chat already exists (ID: {ChatId})", welcomeChat.Id);
                    await AddNewUsersToWelcomeChatAsync(welcomeChat.Id);
                    return;
                }

                _logger.LogInformation("Welcome chat created (ID: {ChatId})", welcomeChat.Id);
                await AddOwnerAdminToChatAsync(welcomeChat.Id);
                await AddAllUsersToWelcomeChatAsync(welcomeChat.Id);
                await CreateWelcomeMessagesAsync(welcomeChat.Id);

                _logger.LogInformation("Welcome chat initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize welcome chat");
                throw;
            }
        }

        private async Task AddAllUsersToWelcomeChatAsync(int chatId)
        {
            var users = await _context.Users.ToListAsync();

            foreach (var user in users)
            {
                var existingMember = await _context.ChatMembers
                    .FirstOrDefaultAsync(item => item.ChatId == chatId && item.UserId == user.Id);

                if (existingMember != null)
                {
                    continue;
                }

                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chatId,
                    UserId = user.Id,
                    Role = ChatMemberRole.Base,
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {UserCount} users to welcome chat", users.Count);
        }

        private async Task AddNewUsersToWelcomeChatAsync(int chatId)
        {
            var allUserIds = await _context.Users.Select(item => item.Id).ToListAsync();
            var existingMembers = await _context.ChatMembers
                .Where(item => item.ChatId == chatId)
                .Select(item => item.UserId)
                .ToListAsync();

            var newUserIds = allUserIds.Except(existingMembers).ToList();
            if (newUserIds.Count == 0)
            {
                return;
            }

            foreach (var userId in newUserIds)
            {
                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chatId,
                    UserId = userId,
                    Role = ChatMemberRole.Base,
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Added {UserCount} new users to welcome chat", newUserIds.Count);
        }

        public Task EnsureUserInWelcomeChatAsync(int userId)
        {
            return _welcomeChatService.EnsureUserInWelcomeChatAsync(_context, userId);
        }

        private async Task AddOwnerAdminToChatAsync(int chatId)
        {
            try
            {
                var adminEmail = _configuration["AdminEmail"];
                User? adminUser = null;

                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    adminUser = await _context.Users
                        .Include(item => item.LoginInfo)
                        .FirstOrDefaultAsync(item => item.LoginInfo != null && item.LoginInfo.Email == adminEmail);
                }

                adminUser ??= await _context.Users.FirstOrDefaultAsync(item => item.Username == "admin");
                adminUser ??= await _context.Users.OrderBy(item => item.CreatedAt).FirstOrDefaultAsync();

                if (adminUser == null)
                {
                    _logger.LogWarning("No suitable admin user found to assign as Creator for welcome chat {ChatId}", chatId);
                    return;
                }

                var existingMember = await _context.ChatMembers
                    .FirstOrDefaultAsync(item => item.ChatId == chatId && item.UserId == adminUser.Id);

                if (existingMember != null)
                {
                    if (existingMember.Role != ChatMemberRole.Creator)
                    {
                        existingMember.Role = ChatMemberRole.Creator;
                        _context.ChatMembers.Update(existingMember);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Promoted user {UserId} to Creator for welcome chat {ChatId}", adminUser.Id, chatId);
                    }
                    else
                    {
                        _logger.LogInformation("User {UserId} is already Creator for welcome chat {ChatId}", adminUser.Id, chatId);
                    }

                    return;
                }

                _context.ChatMembers.Add(new ChatMember
                {
                    ChatId = chatId,
                    UserId = adminUser.Id,
                    Role = ChatMemberRole.Creator,
                    JoinedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                _logger.LogInformation("Assigned user {UserId} as Creator for welcome chat {ChatId}", adminUser.Id, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign owner admin to welcome chat {ChatId}", chatId);
            }
        }

        private async Task CreateWelcomeMessagesAsync(int chatId)
        {
            var adminMember = await _context.ChatMembers
                .Where(item => item.ChatId == chatId && (item.Role == ChatMemberRole.Creator || item.Role == ChatMemberRole.Admin))
                .OrderBy(item => item.JoinedAt)
                .FirstOrDefaultAsync();

            User? admin = null;
            if (adminMember != null)
            {
                admin = await _context.Users.FindAsync(adminMember.UserId);
            }

            admin ??= await _context.Users.FirstOrDefaultAsync(item => item.Username == "admin");
            if (admin == null)
            {
                _logger.LogWarning("No admin user found for sending welcome messages");
                return;
            }

            var welcomeMessages = new[]
            {
                "Welcome to Edemly!\n\n" +
                "We are happy to welcome you to the messenger that combines communication and planning.\n\n" +
                "What to expect:\n" +
                "- Communicate effectively: Exchange messages with colleagues and friends in a convenient interface.\n" +
                "- Plan your time: The built-in planner and calendar will help you organize your tasks and events.\n" +
                "- Customize it for yourself: Choose themes that suit your style.\n\n" +
                "How to get started?\n" +
                "Adding contacts: To add a new contact, use the search function. Enter the user's name or email address and start chatting.\n\n" +
                "All other features can be found in the main menu. Enjoy!"
            };

            var latestSentAt = DateTime.MinValue;
            foreach (var messageText in welcomeMessages)
            {
                var message = new Message
                {
                    ChatId = chatId,
                    SenderId = admin.Id,
                    Text = messageText,
                    Type = MessageType.Txt,
                    SentAt = DateTime.UtcNow
                };

                _context.Messages.Add(message);
                if (message.SentAt > latestSentAt)
                {
                    latestSentAt = message.SentAt;
                }
            }

            await _context.SaveChangesAsync();

            var chat = await _context.Chats.FindAsync(chatId);
            if (chat != null)
            {
                chat.LastMessageTime = latestSentAt == DateTime.MinValue ? DateTime.UtcNow : latestSentAt;
                _context.Chats.Update(chat);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Created {MessageCount} welcome messages (sender user id: {UserId})", welcomeMessages.Length, admin.Id);
        }
    }
}
