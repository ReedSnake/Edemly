namespace Edemly.Server.Api.Services
{
    public interface IPermissionService
    {
        public Task<bool> IsInChat(int userId, int chatId);
        //User
        public bool CanDeleteUser(int userId, int userToDeleteId);
        //Chat
        public Task<bool> CanUpdateChat(int userId, int chatId);
        public Task<bool> CanDeleteChat(int userId, int chatId);
        //Message
        public Task<bool> CanUpdateMessage(int userId, int messageId);
        public Task<bool> CanDeleteMessage(int userId, int messageId);
        //Chat mem
        public Task<bool> CanAddChatMember(int userId, int chatId);
        public Task<bool> CanUpdateChatMember(int userId, int chatMemberId);
        public Task<bool> CanDeleteChatMember(int userId, int chatMemberId);
        //Note
        public Task<bool> IsNoteAuthor(int userId, int noteId);
        //Reminding
        public Task<bool> IsRemindingAuthor(int userId, int remindingId);
    }
}