using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Utils;

namespace Edemly.Server.Api.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly DbContext _ctx;
        private readonly bool _isTenant;

        public PermissionService(ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            _ctx = DbContextResolver.Resolve(out var isTenant, serverDb, tenantProvider, tenantDbFactory);
            _isTenant = isTenant;
        }

        //use this check for chat related gets like chat info, members or messages
        public async Task<bool> IsInChat(int userId, int chatId)
        {
            try
            {
                return await _ctx.Set<ChatMember>().AnyAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        private async Task<string> CheckRights(int userId, int chatId)
        {
            try
            {
                ChatMember? cm = await _ctx.Set<ChatMember>().FirstOrDefaultAsync(cm => cm.UserId == userId && cm.ChatId == chatId);
                ChatMemberRole? role = cm?.Role;

                string rights = "none";
                if (role == ChatMemberRole.Admin) rights = "admin";
                if (role == ChatMemberRole.Creator) rights = "creator";

                return rights;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        //User
        public bool CanDeleteUser(int userId, int userToDeleteId)
        {
            return userId == userToDeleteId; //dont really need this to be here but I'll keep it in case we want admins to be able to delete users or something
        }
        //Chat
        public async Task<bool> CanUpdateChat(int userId, int chatId)
        {
            return await CheckRights(userId, chatId) != "none";
        }
        public async Task<bool> CanDeleteChat(int userId, int chatId)
        {
            return await CheckRights(userId, chatId) == "creator";
        }
        //Message
        public async Task<bool> CanUpdateMessage(int userId, int messageId)
        {
            try
            {
                Message? m = await _ctx.Set<Message>().FirstOrDefaultAsync(m => m.SenderId == userId && m.Id == messageId);
                return m != null;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        public async Task<bool> CanDeleteMessage(int userId, int messageId)
        {
            try
            {
                Message? m = await _ctx.Set<Message>().FindAsync(messageId);
                if (m == null) return false;

                int chatId = m.ChatId;
                if (await CheckRights(userId, chatId) != "none") return true;

                return m.SenderId == userId;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        //Chat mem
        public async Task<bool> CanAddChatMember(int userId, int chatId)
        {
            return await CheckRights(userId, chatId) != "none";
        }
        public async Task<bool> CanUpdateChatMember(int userId, int chatMemberId)
        {
            try
            {
                ChatMember? cm = await _ctx.Set<ChatMember>().FindAsync(chatMemberId);
                if (cm == null) return false;
                if (cm.UserId == userId) return false;

                string userRights = await CheckRights(userId, cm.ChatId);

                if (userRights == "none") return false;
                if (userRights == "admin" && cm.Role != ChatMemberRole.Base) return false;
                return true;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        public async Task<bool> CanDeleteChatMember(int userId, int chatMemberId)
        {
            try
            {
                ChatMember? cm = await _ctx.Set<ChatMember>().FindAsync(chatMemberId);
                if (cm == null) return false;
                if (cm.UserId == userId) return false;

                string userRights = await CheckRights(userId, cm.ChatId);

                if (userRights == "none") return false;
                if (userRights == "admin" && cm.Role != ChatMemberRole.Base) return false;
                return true;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        //Note
        public async Task<bool> IsNoteAuthor(int userId, int noteId)
        {
            try
            {
                Edemly.Server.Data.Entities.Note? n = await _ctx.Set<Note>().FindAsync(noteId);
                if (n == null) return false;
                return n.CreatorId == userId;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
        //Reminding
        public async Task<bool> IsRemindingAuthor(int userId, int remindingId)
        {
            try
            {
                Reminding? r = await _ctx.Set<Reminding>().FindAsync(remindingId);
                if (r == null) return false;
                return r.UserId == userId;
            }
            finally
            {
                if (_isTenant) _ctx.Dispose();
            }
        }
    }
}
