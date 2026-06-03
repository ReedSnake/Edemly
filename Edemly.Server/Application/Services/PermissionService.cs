using Microsoft.EntityFrameworkCore;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;

namespace Edemly.Server.Api.Services
{
    public class PermissionService : TenantAwareServiceBase, IPermissionService
    {
        public PermissionService(ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
            : base(serverDb, tenantProvider, tenantDbFactory)
        {
        }

        public async Task<bool> IsInChat(int userId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            return await ctx.Set<ChatMember>()
                .AsNoTracking()
                .AnyAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
        }

        public bool CanDeleteUser(int userId, int userToDeleteId)
        {
            return userId == userToDeleteId;
        }

        public async Task<bool> CanUpdateChat(int userId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, userId, chatId) != "none";
        }

        public async Task<bool> CanDeleteChat(int userId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, userId, chatId) == "creator";
        }

        public async Task<bool> CanUpdateMessage(int userId, int messageId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var message = await ctx.Set<Message>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SenderId == userId && m.Id == messageId);

            return message != null;
        }

        public async Task<bool> CanDeleteMessage(int userId, int messageId)
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

            if (await CheckRightsAsync(ctx, userId, message.ChatId) != "none")
            {
                return true;
            }

            return message.SenderId == userId;
        }

        public async Task<bool> CanAddChatMember(int userId, int chatId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CheckRightsAsync(dbContextLease.Context, userId, chatId) != "none";
        }

        public async Task<bool> CanUpdateChatMember(int userId, int chatMemberId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CanManageChatMemberAsync(dbContextLease.Context, userId, chatMemberId);
        }

        public async Task<bool> CanDeleteChatMember(int userId, int chatMemberId)
        {
            await using var dbContextLease = ResolveDbContext();
            return await CanManageChatMemberAsync(dbContextLease.Context, userId, chatMemberId);
        }

        public async Task<bool> IsNoteAuthor(int userId, int noteId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var note = await ctx.Set<Note>()
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == noteId);

            return note != null && note.CreatorId == userId;
        }

        public async Task<bool> IsRemindingAuthor(int userId, int remindingId)
        {
            await using var dbContextLease = ResolveDbContext();
            var ctx = dbContextLease.Context;

            var reminding = await ctx.Set<Reminding>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == remindingId);

            return reminding != null && reminding.UserId == userId;
        }

        private static async Task<string> CheckRightsAsync(DbContext ctx, int userId, int chatId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.UserId == userId && cm.ChatId == chatId);

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

        private static async Task<bool> CanManageChatMemberAsync(DbContext ctx, int userId, int chatMemberId)
        {
            var chatMember = await ctx.Set<ChatMember>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.Id == chatMemberId);

            if (chatMember == null || chatMember.UserId == userId)
            {
                return false;
            }

            var userRights = await CheckRightsAsync(ctx, userId, chatMember.ChatId);

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
