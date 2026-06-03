using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;
using Edemly.Contracts.ChatMembers;

namespace Edemly.Server.Api.Services
{
    public class ChatMemberService : TenantAwareServiceBase, IChatMemberService
    {
        private readonly ILogger<ChatMemberService> _logger;

        public ChatMemberService(ServerDbContext serverDb, ILogger<ChatMemberService> logger, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
            _logger = logger;
        }

        public async Task<ServiceMessageResult> AddMember(int currentUserId, CreateChatMemberDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await CanAddChatMemberAsync(ctx, currentUserId, model.ChatId))
                {
                    return ServiceMessageResult.Forbidden();
                }

                var result = await AddMember(ctx, model.ChatId, model.UserId, (ChatMemberRole)model.Role);
                if (!result.Success)
                {
                    return ServiceMessageResult.BadRequest(result.Error ?? "Failed to add member");
                }

                return ServiceMessageResult.Ok("Chat member added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add chat member");
                return ServiceMessageResult.Unexpected("Failed to add chat member");
            }
        }

        public async Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role)
        {
            await using var dbContextLease = ResolveDbContext();
            return await AddMember(dbContextLease.Context, chatId, userId, role);
        }

        public async Task<ServiceMessageResult> UpdateMember(int currentUserId, UpdateChatMemberDto model)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>().FindAsync(model.Id);
                if (member == null || !await CanManageMemberAsync(ctx, currentUserId, member, requireDifferentUser: true))
                {
                    return ServiceMessageResult.Forbidden();
                }

                if (model.Role.HasValue)
                {
                    member.Role = (ChatMemberRole)model.Role.Value;
                }

                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Chat member updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update chat member");
                return ServiceMessageResult.Unexpected("Failed to update chat member");
            }
        }

        public async Task<ServiceMessageResult> DeleteMember(int currentUserId, int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>().FindAsync(id);
                if (member == null || !await CanManageMemberAsync(ctx, currentUserId, member, requireDifferentUser: true))
                {
                    return ServiceMessageResult.Forbidden();
                }

                ctx.Set<ChatMember>().Remove(member);
                await ctx.SaveChangesAsync();
                return ServiceMessageResult.Ok("Chat member removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat member");
                return ServiceMessageResult.Unexpected("Failed to delete chat member");
            }
        }

        public async Task<ServiceDataResult<ChatMemberDto>> GetMember(int id)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var member = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(chatMember => chatMember.Id == id);

                if (member == null)
                {
                    return ServiceDataResult<ChatMemberDto>.NotFound("Member not found");
                }

                return ServiceDataResult<ChatMemberDto>.Ok(ToDto(member));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat member");
                return ServiceDataResult<ChatMemberDto>.Unexpected("Failed to get chat member");
            }
        }

        public async Task<ServiceDataResult<List<ChatMemberDto>>> GetMembers(int currentUserId, int chatId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                if (!await IsInChatAsync(ctx, currentUserId, chatId))
                {
                    return ServiceDataResult<List<ChatMemberDto>>.Forbidden();
                }

                var members = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .Where(m => m.ChatId == chatId)
                    .Select(m => new ChatMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = (int)m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return ServiceDataResult<List<ChatMemberDto>>.Ok(members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chat members");
                return ServiceDataResult<List<ChatMemberDto>>.Unexpected("Failed to get chat members");
            }
        }

        public async Task<ServiceDataResult<List<ChatMemberDto>>> GetMemberships(int currentUserId)
        {
            try
            {
                await using var dbContextLease = ResolveDbContext();
                var ctx = dbContextLease.Context;

                var memberships = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .Where(m => m.UserId == currentUserId)
                    .Select(m => new ChatMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        ChatId = m.ChatId,
                        Role = (int)m.Role,
                        JoinedAt = m.JoinedAt
                    })
                    .ToListAsync();

                return ServiceDataResult<List<ChatMemberDto>>.Ok(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user memberships");
                return ServiceDataResult<List<ChatMemberDto>>.Unexpected("Failed to get user memberships");
            }
        }

        private async Task<(bool Success, string? Error)> AddMember(DbContext ctx, int chatId, int userId, ChatMemberRole role)
        {
            try
            {
                var existingMember = await ctx.Set<ChatMember>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

                if (existingMember != null)
                {
                    _logger.LogInformation("User {UserId} is already a member of chat {ChatId}", userId, chatId);
                    return (true, null);
                }

                var member = new ChatMember
                {
                    UserId = userId,
                    ChatId = chatId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow
                };

                ctx.Set<ChatMember>().Add(member);
                await ctx.SaveChangesAsync();

                _logger.LogInformation("Added user {UserId} to chat {ChatId} with role {Role}", userId, chatId, role);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add user {UserId} to chat {ChatId}", userId, chatId);
                return (false, "Failed to add chat member");
            }
        }

        private static Task<bool> IsInChatAsync(DbContext ctx, int userId, int chatId)
        {
            return ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
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

        private static ChatMemberDto ToDto(ChatMember member)
        {
            return new ChatMemberDto
            {
                Id = member.Id,
                UserId = member.UserId,
                ChatId = member.ChatId,
                Role = (int)member.Role,
                JoinedAt = member.JoinedAt
            };
        }
    }
}
