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
                    .Select(chat => new ChatRow
                    {
                        Id = chat.Id,
                        Name = chat.Name,
                        Description = chat.Description,
                        IconUrl = chat.IconUrl,
                        Type = chat.Type,
                        CreatedAt = chat.CreatedAt,
                        LastMessageTime = chat.LastMessageTime,
                        LastMessageId = chat.LastMessageId,
                        LastMessageText = chat.LastMessageText,
                        LastMessageSenderId = chat.LastMessageSenderId
                    })
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                {
                    _logger.LogInformation(
                        "Found existing private chat {ChatId} between users {CurrentUserId} and {TargetUserId}",
                        existingChat.Id,
                        currentUserId,
                        targetUserId);

                    return ServiceResult<ChatDto>.Ok(existingChat.ToDto(existingChat.Name));
                }

                var targetUserExists = await ctx.Set<User>()
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == targetUserId);

                if (!targetUserExists)
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
                    .Where(chat => chat.ChatMembers.Any(member => member.UserId == currentUserId))
                    .OrderByDescending(chat => chat.LastMessageTime ?? chat.CreatedAt)
                    .Select(chat => new ChatRow
                    {
                        Id = chat.Id,
                        Name = chat.Name,
                        Description = chat.Description,
                        IconUrl = chat.IconUrl,
                        Type = chat.Type,
                        CreatedAt = chat.CreatedAt,
                        LastMessageTime = chat.LastMessageTime,
                        LastMessageId = chat.LastMessageId,
                        LastMessageText = chat.LastMessageText,
                        LastMessageSenderId = chat.LastMessageSenderId
                    })
                    .ToListAsync();

                var directChatIds = chats
                    .Where(chat => chat.Type == ChatType.Direct)
                    .Select(chat => chat.Id)
                    .ToList();

                var directMembers = directChatIds.Count == 0
                    ? new Dictionary<int, DirectChatMemberRow>()
                    : (await GetDirectChatOtherMembersAsync(ctx, directChatIds, currentUserId))
                    .GroupBy(member => member.ChatId)
                    .ToDictionary(group => group.Key, group => group.First());

                var result = chats
                    .Select(chat => chat.ToDto(ResolveDisplayName(chat, directMembers.GetValueOrDefault(chat.Id))))
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
                    .Where(chat => chat.Id == chatId)
                    .Select(chat => new ChatRow
                    {
                        Id = chat.Id,
                        Name = chat.Name,
                        Description = chat.Description,
                        IconUrl = chat.IconUrl,
                        Type = chat.Type,
                        CreatedAt = chat.CreatedAt,
                        LastMessageTime = chat.LastMessageTime,
                        LastMessageId = chat.LastMessageId,
                        LastMessageText = chat.LastMessageText,
                        LastMessageSenderId = chat.LastMessageSenderId,
                        IsCurrentUserMember = chat.ChatMembers.Any(member => member.UserId == currentUserId)
                    })
                    .FirstOrDefaultAsync();

                if (chat == null)
                {
                    return ServiceResult<ChatDto>.NotFound("Chat not found");
                }

                if (!chat.IsCurrentUserMember)
                {
                    return ServiceResult<ChatDto>.Forbidden();
                }

                var otherMember = chat.Type == ChatType.Direct
                    ? await GetDirectChatOtherMemberAsync(ctx, chat.Id, currentUserId)
                    : null;

                return ServiceResult<ChatDto>.Ok(chat.ToDto(ResolveDisplayName(chat, otherMember)));
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
            return ResolveDirectChatDisplayName(user?.Username, user?.FirstName, user?.LastName, targetUserId);
        }

        private static Task<List<DirectChatMemberRow>> GetDirectChatOtherMembersAsync(
            DbContext ctx,
            IReadOnlyCollection<int> directChatIds,
            int currentUserId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(member => directChatIds.Contains(member.ChatId) && member.UserId != currentUserId)
                .Select(member => new DirectChatMemberRow
                {
                    ChatId = member.ChatId,
                    UserId = member.UserId,
                    Username = member.User.Username,
                    FirstName = member.User.FirstName,
                    LastName = member.User.LastName
                })
                .ToListAsync();
        }

        private static Task<DirectChatMemberRow?> GetDirectChatOtherMemberAsync(
            DbContext ctx,
            int chatId,
            int currentUserId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(member => member.ChatId == chatId && member.UserId != currentUserId)
                .Select(member => new DirectChatMemberRow
                {
                    ChatId = member.ChatId,
                    UserId = member.UserId,
                    Username = member.User.Username,
                    FirstName = member.User.FirstName,
                    LastName = member.User.LastName
                })
                .FirstOrDefaultAsync();
        }

        private static string ResolveDisplayName(ChatRow chat, DirectChatMemberRow? otherMember)
        {
            if (chat.Type != ChatType.Direct)
            {
                return chat.Name;
            }

            if (otherMember == null)
            {
                return chat.Name;
            }

            return ResolveDirectChatDisplayName(otherMember.Username, otherMember.FirstName, otherMember.LastName, otherMember.UserId);
        }

        private static string ResolveDirectChatDisplayName(
            string? username,
            string? firstName,
            string? lastName,
            int targetUserId)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                return username;
            }

            var fullName = string.Join(" ", new[] { firstName, lastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return $"User {targetUserId}";
        }

        private sealed class ChatRow
        {
            public int Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string? Description { get; init; }
            public string? IconUrl { get; init; }
            public ChatType Type { get; init; }
            public DateTime CreatedAt { get; init; }
            public DateTime? LastMessageTime { get; init; }
            public int? LastMessageId { get; init; }
            public string? LastMessageText { get; init; }
            public int? LastMessageSenderId { get; init; }
            public bool IsCurrentUserMember { get; init; }

            public ChatDto ToDto(string displayName)
            {
                return new ChatDto
                {
                    Id = Id,
                    Name = displayName,
                    Description = Description ?? string.Empty,
                    IconUrl = IconUrl ?? string.Empty,
                    Type = (int)Type,
                    CreatedAt = CreatedAt,
                    LastMessageTime = LastMessageTime,
                    LastMessageId = LastMessageId,
                    LastMessageText = LastMessageText,
                    LastMessageSenderId = LastMessageSenderId
                };
            }
        }

        private sealed class DirectChatMemberRow
        {
            public int ChatId { get; init; }
            public int UserId { get; init; }
            public string? Username { get; init; }
            public string? FirstName { get; init; }
            public string? LastName { get; init; }
        }
    }
}
