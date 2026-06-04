using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
{
    public class ChatMemberService : TenantAwareServiceBase, IChatMemberService
    {
        private readonly ILogger<ChatMemberService> _logger;

        public ChatMemberService(ServerDbContext serverDbContext, ILogger<ChatMemberService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbContextFactory)
            : base(serverDbContext, tenantProvider, tenantDbContextFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceResult> AddMemberAsync(int requesterId, CreateChatMemberDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await ChatExistsAsync(ctx, request.ChatId))
                {
                    return ServiceResult.NotFound("Chat not found");
                }

                if (!await UserExistsAsync(ctx, request.UserId))
                {
                    return ServiceResult.NotFound("User not found");
                }

                if (!await CanAddChatMemberAsync(ctx, requesterId, request.ChatId))
                {
                    return ServiceResult.Forbidden();
                }

                return await AddMemberAsync(ctx, request.ChatId, request.UserId, (ChatMemberRole)request.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add chat member");
                return ServiceResult.Unexpected("Failed to add chat member");
            }
        }

        public async Task<ServiceResult> AddMemberAsync(int chatId, int targetUserId, ChatMemberRole role)
        {
            await using var dbContextLease = ResolveDbContext();
            return await AddMemberAsync(dbContextLease.Context, chatId, targetUserId, role);
        }

        public async Task<ServiceResult> UpdateAsync(int requesterId, UpdateChatMemberDto request)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>().FindAsync(request.Id);
                if (member == null)
                {
                    return ServiceResult.NotFound("Chat member not found");
                }

                if (!await CanManageMemberAsync(ctx, requesterId, member, requireDifferentUser: true))
                {
                    return ServiceResult.Forbidden();
                }

                if (request.Role.HasValue)
                {
                    member.Role = (ChatMemberRole)request.Role.Value;
                }

                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Chat member updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update chat member");
                return ServiceResult.Unexpected("Failed to update chat member");
            }
        }

        public async Task<ServiceResult> DeleteAsync(int requesterId, int chatMemberId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>().FindAsync(chatMemberId);
                if (member == null)
                {
                    return ServiceResult.NotFound("Chat member not found");
                }

                if (!await CanManageMemberAsync(ctx, requesterId, member, requireDifferentUser: true))
                {
                    return ServiceResult.Forbidden();
                }

                ctx.Set<ChatMember>().Remove(member);
                await ctx.SaveChangesAsync();
                return ServiceResult.Ok("Chat member removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat member");
                return ServiceResult.Unexpected("Failed to delete chat member");
            }
        }

        public async Task<ServiceResult<ChatMemberDto>> GetMemberAsync(int chatMemberId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(chatMember => chatMember.Id == chatMemberId);

                if (member == null)
                {
                    return ServiceResult<ChatMemberDto>.NotFound("Member not found");
                }

                return ServiceResult<ChatMemberDto>.Ok(ChatMemberMappings.ToDto(member));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat member");
                return ServiceResult<ChatMemberDto>.Unexpected("Failed to get chat member");
            }
        }

        public async Task<ServiceResult<List<ChatMemberDto>>> GetMembersAsync(int currentUserId, int chatId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await ChatExistsAsync(ctx, chatId))
                {
                    return ServiceResult<List<ChatMemberDto>>.NotFound("Chat not found");
                }

                if (!await IsInChatAsync(ctx, currentUserId, chatId))
                {
                    return ServiceResult<List<ChatMemberDto>>.Forbidden();
                }

                var members = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .Where(m => m.ChatId == chatId)
                    .Select(ChatMemberMappings.Projection)
                    .ToListAsync();

                return ServiceResult<List<ChatMemberDto>>.Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat members");
                return ServiceResult<List<ChatMemberDto>>.Unexpected("Failed to get chat members");
            }
        }

        public async Task<ServiceResult<List<ChatMemberDto>>> GetMembershipsAsync(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var memberships = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .Where(m => m.UserId == currentUserId)
                    .Select(ChatMemberMappings.Projection)
                    .ToListAsync();

                return ServiceResult<List<ChatMemberDto>>.Ok(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user memberships");
                return ServiceResult<List<ChatMemberDto>>.Unexpected("Failed to get user memberships");
            }
        }

        private async Task<ServiceResult> AddMemberAsync(DbContext ctx, int chatId, int targetUserId, ChatMemberRole role)
        {
            try
            {
                var existingMember = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(chatMember => chatMember.ChatId == chatId && chatMember.UserId == targetUserId);

                if (existingMember != null)
                {
                    _logger.LogInformation("User {UserId} is already a member of chat {ChatId}", targetUserId, chatId);
                    return ServiceResult.Conflict("User is already a member of this chat");
                }

                var member = new ChatMember
                {
                    UserId = targetUserId,
                    ChatId = chatId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow
                };

                ctx.Set<ChatMember>().Add(member);
                await ctx.SaveChangesAsync();

                _logger.LogInformation("Added user {UserId} to chat {ChatId} with role {Role}", targetUserId, chatId, role);
                return ServiceResult.Ok("Chat member added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add user {UserId} to chat {ChatId}", targetUserId, chatId);
                return ServiceResult.Unexpected("Failed to add chat member");
            }
        }

        private static Task<bool> ChatExistsAsync(DbContext ctx, int chatId)
        {
            return ctx.Set<Chat>()
                .AsNoTracking()
                .AnyAsync(chat => chat.Id == chatId);
        }

        private static Task<bool> UserExistsAsync(DbContext ctx, int targetUserId)
        {
            return ctx.Set<User>()
                .AsNoTracking()
                .AnyAsync(user => user.Id == targetUserId);
        }

        private static Task<bool> IsInChatAsync(DbContext ctx, int currentUserId, int chatId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(cm => cm.UserId == currentUserId && cm.ChatId == chatId);
        }

        private static async Task<bool> CanAddChatMemberAsync(DbContext ctx, int currentUserId, int chatId)
        {
            var currentMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == chatId);

            if (currentMember == null)
            {
                return false;
            }

            return currentMember.Role == ChatMemberRole.Admin || currentMember.Role == ChatMemberRole.Creator;
        }

        private static async Task<bool> CanManageMemberAsync(DbContext ctx, int currentUserId, ChatMember member, bool requireDifferentUser)
        {
            if (requireDifferentUser && member.UserId == currentUserId)
            {
                return false;
            }

            var currentMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == member.ChatId);

            if (currentMember == null)
            {
                return false;
            }

            if (currentMember.Role == ChatMemberRole.Creator)
            {
                return true;
            }

            if (currentMember.Role == ChatMemberRole.Admin)
            {
                return member.Role == ChatMemberRole.Base;
            }

            return false;
        }
    }
}