using Edemly.Contracts.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.ChatMembers;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Common.Mappers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Chats
{
    public class ChatService : TenantAwareServiceBase, IChatService
    {
        private readonly IChatMemberService _chatMemberService;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ServerDbContext serverDbContext,
            IChatMemberService chatMemberService,
            ILogger<ChatService> logger,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _chatMemberService = chatMemberService;
            _logger = logger;
        }

        public async Task<ServiceResult<ChatDto>> CreateOrGetPrivateChatAsync(int currentUserId, int targetUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (currentUserId == targetUserId)
                {
                    return ServiceResult<ChatDto>.BadRequest("Cannot create chat with yourself");
                }

                var existingChat = await ctx.Set<Chat>()
                    .AsNoTracking()
                    .Where(c => c.Type == ChatType.Direct)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == currentUserId))
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == targetUserId))
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                {
                    _logger.LogInformation(
                        "Found existing private chat {ChatId} between users {CurrentUserId} and {TargetUserId}",
                        existingChat.Id,
                        currentUserId,
                        targetUserId);

                    return ServiceResult<ChatDto>.Ok(ChatMappings.ToDto(existingChat));
                }

                var targetUser = await ctx.Set<User>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Id == targetUserId);

                if (targetUser == null)
                {
                    return ServiceResult<ChatDto>.NotFound("User not found");
                }

                var newChat = new Chat
                {
                    Name = "Private chat",
                    Type = ChatType.Direct,
                    CreatedAt = DateTime.UtcNow
                };

                ctx.Set<Chat>().Add(newChat);
                await ctx.SaveChangesAsync();

                await _chatMemberService.AddMemberAsync(newChat.Id, currentUserId, ChatMemberRole.Base);
                await _chatMemberService.AddMemberAsync(newChat.Id, targetUserId, ChatMemberRole.Base);

                _logger.LogInformation(
                    "Created new private chat {ChatId} between users {CurrentUserId} and {TargetUserId}",
                    newChat.Id,
                    currentUserId,
                    targetUserId);

                return ServiceResult<ChatDto>.Ok(ChatMappings.ToDto(newChat));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating or getting private chat");
                return ServiceResult<ChatDto>.Unexpected("Failed to create private chat");
            }
        }

        public async Task<ServiceResult<ChatDto>> CreateGroupChatAsync(
            int requesterId,
            string groupName,
            List<int> participantIds)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return ServiceResult<ChatDto>.BadRequest("Group name is required");
            }

            if (participantIds == null || participantIds.Count == 0)
            {
                return ServiceResult<ChatDto>.BadRequest("At least one participant is required");
            }

            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var distinctParticipantIds = participantIds
                    .Where(participantId => participantId != requesterId)
                    .Distinct()
                    .ToList();

                var allMemberIds = distinctParticipantIds
                    .Append(requesterId)
                    .ToList();

                _logger.LogInformation(
                    "Creating group chat '{GroupName}' by user {CreatorId}",
                    groupName,
                    requesterId);

                var validUsers = await ctx.Set<User>()
                    .AsNoTracking()
                    .Where(u => allMemberIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validUsers.Count != allMemberIds.Count)
                {
                    var invalidIds = allMemberIds.Except(validUsers);
                    return ServiceResult<ChatDto>.NotFound(
                        $"Users not found: {string.Join(", ", invalidIds)}");
                }

                var newChat = new Chat
                {
                    Name = groupName.Trim(),
                    Type = ChatType.Group,
                    CreatedAt = DateTime.UtcNow,
                    LastMessageTime = DateTime.UtcNow
                };

                ctx.Set<Chat>().Add(newChat);
                await ctx.SaveChangesAsync();

                await _chatMemberService.AddMemberAsync(
                    newChat.Id,
                    requesterId,
                    ChatMemberRole.Admin);

                foreach (var participantId in distinctParticipantIds)
                {
                    await _chatMemberService.AddMemberAsync(
                        newChat.Id,
                        participantId,
                        ChatMemberRole.Base);
                }

                _logger.LogInformation(
                    "Created group chat {ChatId} '{GroupName}' with {ParticipantCount} participants",
                    newChat.Id,
                    groupName,
                    allMemberIds.Count);

                return ServiceResult<ChatDto>.Ok(ChatMappings.ToDto(newChat));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group chat '{GroupName}'", groupName);
                return ServiceResult<ChatDto>.Unexpected("Failed to create group chat");
            }
        }

        public async Task<ServiceResult<List<ChatDto>>> GetMyChatsAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var chats = await ctx.Set<Chat>()
                    .AsNoTracking()
                    .Include(c => c.ChatMembers)
                        .ThenInclude(cm => cm.User)
                    .Where(c => c.ChatMembers.Any(cm => cm.UserId == currentUserId))
                    .ToListAsync();

                var result = new List<ChatDto>();

                foreach (var chat in chats)
                {
                    var displayName = ResolveDisplayName(chat, currentUserId);
                    result.Add(ChatMappings.ToDto(chat, displayName));
                }

                result = result
                    .OrderByDescending(c => c.LastMessageTime ?? c.CreatedAt)
                    .ToList();

                _logger.LogInformation("Returning {Count} chats for user {UserId}", result.Count, currentUserId);
                return ServiceResult<List<ChatDto>>.Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user chats");
                return ServiceResult<List<ChatDto>>.Unexpected("Failed to get chats");
            }
        }

        public async Task<ServiceResult<ChatDto>> GetByIdAsync(int currentUserId, int chatId)
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
                    return ServiceResult<ChatDto>.NotFound("Chat not found");
                }

                if (!chat.ChatMembers.Any(member => member.UserId == currentUserId))
                {
                    return ServiceResult<ChatDto>.Forbidden();
                }

                return ServiceResult<ChatDto>.Ok(ChatMappings.ToDto(chat, ResolveDisplayName(chat, currentUserId)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat by id {ChatId} for user {UserId}", chatId, currentUserId);
                return ServiceResult<ChatDto>.Unexpected("Failed to get chat");
            }
        }

        public async Task<ServiceResult> UpdateAsync(int chatId, string? name, string? description, string? iconUrl)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var chat = await ctx.Set<Chat>().FindAsync(chatId);

                if (chat == null)
                {
                    return ServiceResult.NotFound("Chat not found");
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
                return ServiceResult.Ok("Chat updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat {ChatId}", chatId);
                return ServiceResult.Unexpected("Failed to update chat");
            }
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

        private static string ResolveDirectChatDisplayName(User? user, int targetUserId)
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

            return $"User {targetUserId}";
        }
    }
}
