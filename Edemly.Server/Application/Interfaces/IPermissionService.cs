namespace Edemly.Server.Api.Services
{
    public interface IPermissionService
    {
        public Task<bool> IsInChatAsync(int currentUserId, int chatId);
        //User
        public bool CanDeleteUser(int requesterId, int targetUserId);
        //Chat
        public Task<bool> CanUpdateChatAsync(int currentUserId, int chatId);
        public Task<bool> CanDeleteChatAsync(int currentUserId, int chatId);
        //Message
        public Task<bool> CanUpdateMessageAsync(int currentUserId, int messageId);
        public Task<bool> CanDeleteMessageAsync(int requesterId, int messageId);
        //Chat mem
        public Task<bool> CanAddChatMemberAsync(int requesterId, int chatId);
        public Task<bool> CanUpdateChatMemberAsync(int requesterId, int chatMemberId);
        public Task<bool> CanDeleteChatMemberAsync(int requesterId, int chatMemberId);
        //Note
        public Task<bool> IsNoteAuthorAsync(int currentUserId, int noteId);
        //Reminding
        public Task<bool> IsRemindingAuthorAsync(int currentUserId, int remindingId);
    }
}
