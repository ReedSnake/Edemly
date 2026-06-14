using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Common
{
    public class PermissionService : TenantAwareServiceBase, IPermissionService
    {
        public PermissionService(ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
        }

        public async Task<bool> IsInChatAsync(int currentUserId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            return await ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(cm => cm.UserId == currentUserId && cm.ChatId == chatId);
        }

        public bool CanDeleteUser(int requesterId, int targetUserId)
        {
            return requesterId == targetUserId;
        }

        public async Task<bool> CanUpdateChatAsync(int currentUserId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, currentUserId, chatId) != "none";
        }

        public async Task<bool> CanDeleteChatAsync(int currentUserId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, currentUserId, chatId) == "creator";
        }

        public async Task<bool> CanUpdateMessageAsync(int currentUserId, int messageId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            return await ctx.Set<Message>()
                .AsNoTracking()
                .AnyAsync(m => m.SenderId == currentUserId && m.Id == messageId);
        }

        public async Task<bool> CanDeleteMessageAsync(int requesterId, int messageId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var message = await ctx.Set<Message>()
                .AsNoTracking()
                .Where(m => m.Id == messageId)
                .Select(m => new MessagePermissionRow
                {
                    ChatId = m.ChatId,
                    SenderId = m.SenderId
                })
                .FirstOrDefaultAsync();

            if (message == null)
            {
                return false;
            }

            if (await CheckRightsAsync(ctx, requesterId, message.ChatId) != "none")
            {
                return true;
            }

            return message.SenderId == requesterId;
        }

        public async Task<bool> CanAddChatMemberAsync(int requesterId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, requesterId, chatId) != "none";
        }

        public async Task<bool> CanUpdateChatMemberAsync(int requesterId, int chatMemberId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CanManageChatMemberAsync(dbContextLease.Context, requesterId, chatMemberId);
        }

        public async Task<bool> CanDeleteChatMemberAsync(int requesterId, int chatMemberId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CanManageChatMemberAsync(dbContextLease.Context, requesterId, chatMemberId);
        }

        public async Task<bool> IsNoteAuthorAsync(int currentUserId, int noteId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            return await ctx.Set<Note>()
                .AsNoTracking()
                .AnyAsync(n => n.Id == noteId && n.CreatorId == currentUserId);
        }

        public async Task<bool> IsRemindingAuthorAsync(int currentUserId, int remindingId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            return await ctx.Set<Reminding>()
                .AsNoTracking()
                .AnyAsync(r => r.Id == remindingId && r.UserId == currentUserId);
        }

        private static async Task<string> CheckRightsAsync(DbContext ctx, int currentUserId, int chatId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(cm => cm.UserId == currentUserId && cm.ChatId == chatId)
                .Select(cm => (ChatMemberRole?)cm.Role)
                .FirstOrDefaultAsync();

            if (chatMember == ChatMemberRole.Creator)
            {
                return "creator";
            }

            if (chatMember == ChatMemberRole.Admin)
            {
                return "admin";
            }

            return "none";
        }

        private static async Task<bool> CanManageChatMemberAsync(DbContext ctx, int requesterId, int chatMemberId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .Where(cm => cm.Id == chatMemberId)
                .Select(cm => new ChatMemberPermissionRow
                {
                    ChatId = cm.ChatId,
                    UserId = cm.UserId,
                    Role = cm.Role
                })
                .FirstOrDefaultAsync();

            if (chatMember == null || chatMember.UserId == requesterId)
            {
                return false;
            }

            var userRights = await CheckRightsAsync(ctx, requesterId, chatMember.ChatId);

            if (userRights == "none")
            {
                return false;
            }

            if (userRights == "admin" && chatMember.Role != ChatMemberRole.Base)
            {
                return false;
            }

            return true;
        }

        private sealed class MessagePermissionRow
        {
            public int ChatId { get; init; }
            public int SenderId { get; init; }
        }

        private sealed class ChatMemberPermissionRow
        {
            public int ChatId { get; init; }
            public int UserId { get; init; }
            public ChatMemberRole Role { get; init; }
        }
    }
}
