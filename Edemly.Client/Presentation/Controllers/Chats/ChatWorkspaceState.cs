#nullable enable

using System;
using System.Collections.Generic;
using Edemly.Client.Models;

namespace Edemly.Client.Presentation.Controllers.Chats
{
    internal sealed class ChatWorkspaceState
    {
        public Dictionary<int, Contact> Contacts { get; } = new();
        public Dictionary<int, List<MessageDto>> ChatHistory { get; } = new();
        public Dictionary<int, int> ChatToUserMap { get; } = new();
        public Dictionary<int, DateTime?> LastMessageDate { get; } = new();
        public Dictionary<int, DateTime> ChatLastMessageTime { get; } = new();
        public Dictionary<int, MessageDto> ChatLastMessage { get; } = new();
        public HashSet<int> ChatsWithUnreadMessages { get; } = new();
        public Dictionary<int, int> ChatTypes { get; } = new();
        public Dictionary<int, Contact> GroupContacts { get; } = new();
        public Dictionary<int, string> UserNamesCache { get; } = new();
        public Dictionary<int, int> ChatLoadedPages { get; } = new();
        public HashSet<int> LoadingOlderChats { get; } = new();
        public HashSet<int> NoMoreOlderMessages { get; } = new();
        public Dictionary<int, (bool IsOnline, DateTime? LastSeenUtc, DateTime ExpiresAtUtc)> UserStatusCache { get; } = new();
        public object StatusCacheLock { get; } = new();

        public Contact? CurrentChatContact { get; set; }
        public int CurrentChatId { get; set; } = -1;
    }
}
