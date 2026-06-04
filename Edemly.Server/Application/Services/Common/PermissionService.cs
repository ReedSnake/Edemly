using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
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

            var message = await ctx.Set<Message>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SenderId == currentUserId && m.Id == messageId);

            return message != null;
        }

        public async Task<bool> CanDeleteMessageAsync(int requesterId, int messageId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var message = await ctx.Set<Message>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == messageId);

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

            var note = await ctx.Set<Note>()
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == noteId);

            return note != null && note.CreatorId == currentUserId;
        }

        public async Task<bool> IsRemindingAuthorAsync(int currentUserId, int remindingId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var reminding = await ctx.Set<Reminding>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == remindingId);

            return reminding != null && reminding.UserId == currentUserId;
        }

        private static async Task<string> CheckRightsAsync(DbContext ctx, int currentUserId, int chatId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.UserId == currentUserId && cm.ChatId == chatId);

            var role = chatMember?.Role;

            if (role == ChatMemberRole.Creator)
            {
                return "creator";
            }

            if (role == ChatMemberRole.Admin)
            {
                return "admin";
            }

            return "none";
        }

        private static async Task<bool> CanManageChatMemberAsync(DbContext ctx, int requesterId, int chatMemberId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.Id == chatMemberId);

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
    }
}