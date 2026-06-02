using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Services
{
    /// <summary>
    /// Сервіс для створення привітального чату при старті сервера
    /// </summary>
    public class WelcomeChatInitializer
    {
        private readonly ServerDbContext _context;
        private readonly ILogger<WelcomeChatInitializer> _logger;
        private readonly IConfiguration _configuration;
        
        private const string WELCOME_CHAT_NAME = "Edemly";
        private const string WELCOME_CHAT_DESCRIPTION = "Official Edemly chat";
        private const string WELCOME_CHAT_ICON = "pack://application:,,,/Assets/logo.png";

        public WelcomeChatInitializer(ServerDbContext context, ILogger<WelcomeChatInitializer> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Створює привітальний чат якщо його ще не існує
        /// </summary>
        public async Task InitializeWelcomeChatAsync()
        {
            try
            {
                _logger.LogInformation("Checking for welcome chat...");

                // Перевіряємо чи вже існує привітальний чат
                var existingChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.Name == WELCOME_CHAT_NAME && c.Type == ChatType.Group);

                if (existingChat != null)
                {
                    _logger.LogInformation($"Welcome chat already exists (ID: {existingChat.Id})");
                    
                    // Додаємо нових користувачів до існуючого чату
                    await AddNewUsersToWelcomeChatAsync(existingChat.Id);
                    return;
                }

                // Створюємо новий привітальний чат
                var welcomeChat = new Chat
                {
                    Name = WELCOME_CHAT_NAME,
                    Description = WELCOME_CHAT_DESCRIPTION,
                    IconUrl = WELCOME_CHAT_ICON,
                    Type = ChatType.Group, // Group - груповий чат
                    CreatedAt = DateTime.UtcNow
                };

                _context.Chats.Add(welcomeChat);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Welcome chat created (ID: {welcomeChat.Id})");

                // Ensure real admin is owner/creator of welcome chat
                await AddOwnerAdminToChatAsync(welcomeChat.Id);

                // Додаємо всіх користувачів до чату
                await AddAllUsersToWelcomeChatAsync(welcomeChat.Id);

                // Створюємо привітальні повідомлення
                // Створюємо привітальні повідомлення лише при первісному створенні чату
                await CreateWelcomeMessagesAsync(welcomeChat.Id);

                _logger.LogInformation("Welcome chat initialization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize welcome chat");
                throw;
            }
        }

        /// <summary>
        /// Додає всіх користувачів до привітального чату
        /// </summary>
        private async Task AddAllUsersToWelcomeChatAsync(int chatId)
        {
            var users = await _context.Users.ToListAsync();
            
            foreach (var user in users)
            {
                var existingMember = await _context.ChatMembers
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == user.Id);

                if (existingMember == null)
                {
                    var member = new ChatMember
                    {
                        ChatId = chatId,
                        UserId = user.Id,
                        Role = ChatMemberRole.Base, // Base - звичайний учасник
                        JoinedAt = DateTime.UtcNow
                    };

                    _context.ChatMembers.Add(member);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Added {users.Count} users to welcome chat");
        }

        /// <summary>
        /// Додає нових користувачів до існуючого привітального чату
        /// (Does NOT send welcome messages on join)
        /// </summary>
        private async Task AddNewUsersToWelcomeChatAsync(int chatId)
        {
            // Знаходимо користувачів які ще не є членами чату
            var allUsers = await _context.Users.Select(u => u.Id).ToListAsync();
            var existingMembers = await _context.ChatMembers
                .Where(cm => cm.ChatId == chatId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var newUserIds = allUsers.Except(existingMembers).ToList();

            if (newUserIds.Count > 0)
            {
                foreach (var userId in newUserIds)
                {
                    var member = new ChatMember
                    {
                        ChatId = chatId,
                        UserId = userId,
                        Role = ChatMemberRole.Base, // Base - звичайний учасник
                        JoinedAt = DateTime.UtcNow
                    };

                    _context.ChatMembers.Add(member);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Added {newUserIds.Count} new users to welcome chat");
            }
        }

        /// <summary>
        /// Ensure single user is a member of the welcome chat.
        /// Call this after creating a new user to add them immediately.
        /// This method does NOT send welcome messages when adding a user.
        /// </summary>
        public async Task EnsureUserInWelcomeChatAsync(int userId)
        {
            // Find or create welcome chat
            var chat = await _context.Chats
                .FirstOrDefaultAsync(c => c.Name == WELCOME_CHAT_NAME && c.Type == ChatType.Group);

            if (chat == null)
            {
                _logger.LogInformation("Welcome chat not found, creating it now...");
                await InitializeWelcomeChatAsync();
                chat = await _context.Chats.FirstOrDefaultAsync(c => c.Name == WELCOME_CHAT_NAME && c.Type == ChatType.Group);
                if (chat == null)
                {
                    _logger.LogError("Failed to create or locate welcome chat");
                    return;
                }
            }

            var exists = await _context.ChatMembers
                .AnyAsync(cm => cm.ChatId == chat.Id && cm.UserId == userId);

            if (exists)
            {
                _logger.LogDebug($"User {userId} is already a member of welcome chat (ID: {chat.Id})");
                return;
            }

            var member = new ChatMember
            {
                ChatId = chat.Id,
                UserId = userId,
                Role = ChatMemberRole.Base,
                JoinedAt = DateTime.UtcNow
            };

            _context.ChatMembers.Add(member);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added user {userId} to welcome chat (ID: {chat.Id})");
        }

        /// <summary>
        /// Adds configured/real admin as Creator of the welcome chat (if found). Falls back to username 'admin' or oldest user.
        /// </summary>
        private async Task AddOwnerAdminToChatAsync(int chatId)
        {
            try
            {
                string? adminEmail = _configuration["AdminEmail"];

                User? adminUser = null;

                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    adminUser = await _context.Users
                        .Include(u => u.LoginInfo)
                        .FirstOrDefaultAsync(u => u.LoginInfo != null && u.LoginInfo.Email == adminEmail);
                }

                if (adminUser == null)
                {
                    adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
                }

                if (adminUser == null)
                {
                    // fallback: pick the earliest created user
                    adminUser = await _context.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();
                }

                if (adminUser == null)
                {
                    _logger.LogWarning("No suitable admin user found to assign as Creator for welcome chat {ChatId}", chatId);
                    return;
                }

                var existingMember = await _context.ChatMembers.FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == adminUser.Id);

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

                var member = new ChatMember
                {
                    ChatId = chatId,
                    UserId = adminUser.Id,
                    Role = ChatMemberRole.Creator,
                    JoinedAt = DateTime.UtcNow
                };

                _context.ChatMembers.Add(member);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Assigned user {UserId} as Creator for welcome chat {ChatId}", adminUser.Id, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to assign owner admin to welcome chat {ChatId}", chatId);
            }
        }

        /// <summary>
        /// Створює привітальні повідомлення у чаті
        /// Chooses first Creator/Admin chat member as sender; falls back to user with username 'admin'.
        /// Also updates chat.LastMessageTime so clients see the message.
        /// </summary>
        private async Task CreateWelcomeMessagesAsync(int chatId)
        {
            // Find first chat member that is Creator or Admin to act as sender
            var adminMember = await _context.ChatMembers
                .Where(cm => cm.ChatId == chatId && (cm.Role == ChatMemberRole.Creator || cm.Role == ChatMemberRole.Admin))
                .OrderBy(cm => cm.JoinedAt)
                .FirstOrDefaultAsync();

            User? admin = null;

            if (adminMember != null)
            {
                admin = await _context.Users.FindAsync(adminMember.UserId);
            }

            // fallback: look up user with username 'admin'
            if (admin == null)
            {
                admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            }

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
                "• Communicate effectively: Exchange messages with colleagues and friends in a convenient interface.\n" +
                "• Plan your time: The built-in planner and calendar will help you organize your tasks and events.\n" +
                "• Customize it for yourself: Choose themes that suit your style.\n\n" +
                "How to get started?\n" +
                "Adding contacts: To add a new contact, use the search function. Enter the user's name or email address and start chatting.\n\n" +
                "All other features can be found in the main menu. Enjoy!"
            };

            DateTime latest = DateTime.MinValue;

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

                if (message.SentAt > latest) latest = message.SentAt;
            }

            await _context.SaveChangesAsync();

            // Update chat last message time so clients display last message and it appears in lists
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat != null)
            {
                chat.LastMessageTime = latest == DateTime.MinValue ? DateTime.UtcNow : latest;
                _context.Chats.Update(chat);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Created {welcomeMessages.Length} welcome messages");
            }

            _logger.LogInformation($"Created {welcomeMessages.Length} welcome messages (sender user id: {admin.Id})");
        }
    }
}