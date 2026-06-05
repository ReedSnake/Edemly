#nullable enable

using System;
using Edemly.Client.Models;

namespace Edemly.Client.Presentation.Rendering.Chats
{
    internal sealed class ChatListItemState
    {
        public required Contact Contact { get; init; }
        public required int ChatId { get; init; }
        public string? LastMessageText { get; init; }
        public string? LastMessageSender { get; init; }
        public required bool HasUnread { get; init; }
        public required bool IsOnline { get; init; }
        public required bool IsActive { get; init; }
        public DateTime? LastMessageTime { get; init; }
    }
}
