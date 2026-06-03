using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Contracts.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Api.Services
{
    public class ChatService : TenantAwareServiceBase, IChatService
    {
        private readonly IChatMemberService _chatMemberService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ServerDbContext serverDb,
            IChatMemberService chatMemberService,
            ILogger<ChatService> logger,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _chatMemberService = chatMemberService;
            _logger = logger;
        }

        public async Task<ServiceDataResult<ChatDto>> CreateOrGetPrivateChat(int currentUserId, int otherUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (currentUserId == otherUserId)
                {
                    return ServiceDataResult<ChatDto>.BadRequest("Cannot create chat with yourself");
                }

                var existingChat = await ctx.Set<Chat>()
                    .AsNoTracking()
                    .Where(c => c.Type == ChatType.Direct)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == currentUserId))
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == otherUserId))
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                {
                    _logger.LogInformation(
                        "Found existing private chat {ChatId} between users {CurrentUserId} and {OtherUserId}",
                        existingChat.Id,
                        currentUserId,
                        otherUserId);

                    return ServiceDataResult<ChatDto>.Ok(ToChatDto(existingChat));
                }

                var otherUser = await ctx.Set<User>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == otherUserId);

                if (otherUser == null)
                {
                    return ServiceDataResult<ChatDto>.BadRequest("User not found");
                }

                var newChat = new Chat
                {
                    Name = "Private chat",
                    Type = ChatType.Direct,
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Set<Chat>().Add(newChat);
                await ctx.SaveChangesAsync();

                await _chatMemberService.AddMember(newChat.Id, currentUserId, ChatMemberRole.Base);
                await _chatMemberService.AddMember(newChat.Id, otherUserId, ChatMemberRole.Base);

                _logger.LogInformation(
                    "Created new private chat {ChatId} between users {CurrentUserId} and {OtherUserId}",
                    newChat.Id,
                    currentUserId,
                    otherUserId);

                return ServiceDataResult<ChatDto>.Ok(ToChatDto(newChat));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or getting private chat");
                return ServiceDataResult<ChatDto>.Unexpected("Failed to create private chat");
            }
        }

        public async Task<ServiceDataResult<ChatDto>> CreateGroupChat(
            int creatorId,
            string groupName,
            List<int> participantIds)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return ServiceDataResult<ChatDto>.BadRequest("Group name cannot be empty");
                }

                if (participantIds == null || participantIds.Count == 0)
                {
                    return ServiceDataResult<ChatDto>.BadRequest("Group must have at least one participant");
                }

                _logger.LogInformation(
                    "Creating group chat '{GroupName}' by user {CreatorId}",
                    groupName,
                    creatorId);

                var validUsers = await ctx.Set<User>()
                    .AsNoTracking()
                    .Where(u => participantIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validUsers.Count != participantIds.Count)
                {
                    var invalidIds = participantIds.Except(validUsers);
                    return ServiceDataResult<ChatDto>.BadRequest($"Users not found: {string.Join(", ", invalidIds)}");
                }

                var newChat = new Chat
                {
                    Name = groupName,
                    Type = ChatType.Group,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageTime = DateTime.UtcNow
                };

                ctx.Set<Chat>().Add(newChat);
                await ctx.SaveChangesAsync();

                await _chatMemberService.AddMember(newChat.Id, creatorId, ChatMemberRole.Admin);

                foreach (var userId in participantIds)
                {
                    if (userId != creatorId)
                    {
                        await _chatMemberService.AddMember(newChat.Id, userId, ChatMemberRole.Base);
                    }
                }

                _logger.LogInformation(
                    "Created group chat {ChatId} '{GroupName}' with {ParticipantCount} participants",
                    newChat.Id,
                    groupName,
                    participantIds.Count);

                return ServiceDataResult<ChatDto>.Ok(ToChatDto(newChat));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group chat '{GroupName}'", groupName);
                return ServiceDataResult<ChatDto>.Unexpected("Failed to create group chat");
            }
        }

        public async Task<ServiceDataResult<List<ChatDto>>> GetMyChats(int userId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var chats = await ctx.Set<Chat>()
                    .AsNoTracking()
                    .Include(c => c.ChatMembers)
                        .ThenInclude(cm => cm.User)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == userId))
                    .ToListAsync();

                var result = new List<ChatDto>();

                foreach (var chat in chats)
                {
                    var displayName = ResolveDisplayName(chat, userId);

                    var lastMessage = await ctx.Set<Message>()
                        .AsNoTracking()
                        .Where(m => m.ChatId == chat.Id)
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefaultAsync();

                    result.Add(ToChatDto(chat, displayName, lastMessage));
                }

                result = result
                    .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                    .ToList();

                _logger.LogInformation("Returning {Count} chats for user {UserId}", result.Count, userId);
                return ServiceDataResult<List<ChatDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chats");
                return ServiceDataResult<List<ChatDto>>.Unexpected("Failed to get chats");
            }
        }

        public async Task<ServiceDataResult<ChatDto>> GetById(int currentUserId, int chatId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var chat = await ctx.Set<Chat>()
                    .AsNoTracking()
                    .Include(c => c.ChatMembers)
                        .ThenInclude(cm => cm.User)
                    .FirstOrDefaultAsync(c => c.Id == chatId);

                if (chat == null)
                {
                    return ServiceDataResult<ChatDto>.NotFound("Chat not found");
                }

                if (!chat.ChatMembers.Any(member => member.UserId == currentUserId))
                {
                    return ServiceDataResult<ChatDto>.Forbidden();
                }

                return ServiceDataResult<ChatDto>.Ok(ToChatDto(chat, ResolveDisplayName(chat, currentUserId)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat by id {ChatId} for user {UserId}", chatId, currentUserId);
                return ServiceDataResult<ChatDto>.Unexpected("Failed to get chat");
            }
        }

        public async Task<ServiceMessageResult> UpdateChat(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var chat = await ctx.Set<Chat>().FindAsync(chatId);

                if (chat == null)
                {
                    return ServiceMessageResult.BadRequest("Chat not found");
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

                await ctx.SaveChangesAsync();

                _logger.LogInformation("Updated chat {ChatId}", chatId);
                return ServiceMessageResult.Ok("Chat updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat {ChatId}", chatId);
                return ServiceMessageResult.Unexpected("Failed to update chat");
            }
        }

        private static ChatDto ToChatDto(Chat chat, string? displayName = null, Message? lastMessage = null)
        {
            return new ChatDto
            {
                Id = chat.Id,
                Name = displayName ?? chat.Name,
                Description = chat.Description ?? string.Empty,
                IconUrl = chat.IconUrl ?? string.Empty,
                Type = (int)chat.Type,
                CreatedAt = chat.CreatedAt,
                LastMessageTime = chat.LastMessageTime,
                LastMessageText = lastMessage?.Text,
                LastMessageSenderId = lastMessage?.SenderId
            };
        }

        private static string ResolveDisplayName(Chat chat, int requestingUserId)
        {
            if (chat.Type != ChatType.Direct)
            {
                return chat.Name;
            }

            var otherMember = chat.ChatMembers.FirstOrDefault(member => member.UserId != requestingUserId);
            if (otherMember == null)
            {
                return chat.Name;
            }

            return ResolveDirectChatDisplayName(otherMember.User, otherMember.UserId);
        }

        private static string ResolveDirectChatDisplayName(User? user, int userId)
        {
            if (!string.IsNullOrWhiteSpace(user?.Username))
            {
                return user.Username;
            }

            var fullName = string.Join(" ", new[] { user?.FirstName, user?.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return $"User {userId}";
        }
    }
}
