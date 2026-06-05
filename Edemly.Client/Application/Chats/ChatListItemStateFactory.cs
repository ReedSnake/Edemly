#nullable enable

using System;
using Edemly.Client.Models;
using Edemly.Client.Presentation.Controllers.Chats;
using Edemly.Client.Presentation.Rendering.Chats;

namespace Edemly.Client.Application.Chats
{
    internal sealed class ChatListItemStateFactory
    {
        private readonly ChatWorkspaceState _runtimeState;
        private readonly int _currentUserId;

        public ChatListItemStateFactory(ChatWorkspaceState runtimeState, int currentUserId)
        {
            _runtimeState = runtimeState;
            _currentUserId = currentUserId;
        }

        public DateTime GetLastActivity(int chatId)
        {
            DateTime last = DateTime.MinValue;

            if (_runtimeState.ChatLastMessage.TryGetValue(chatId, out var lastMessage) && lastMessage != null)
            {
                last = lastMessage.SentAt;
            }

            if (_runtimeState.ChatLastMessageTime.TryGetValue(chatId, out var time) && time > last)
            {
                last = time;
            }

            return last;
        }

        public bool TryGetContact(int chatId, out Contact? contact)
        {
            contact = null;

            if (!_runtimeState.ChatToUserMap.TryGetValue(chatId, out var userId))
            {
                return false;
            }

            if (userId < 0)
            {
                return _runtimeState.GroupContacts.TryGetValue(chatId, out contact);
            }

            return _runtimeState.Contacts.TryGetValue(userId, out contact);
        }

        public bool TryCreate(
            int chatId,
            bool suppressGroupSenderName,
            bool isOnline,
            bool isActive,
            out ChatListItemState? itemState)
        {
            itemState = null;

            if (!TryGetContact(chatId, out var contact) || contact == null)
            {
                return false;
            }

            itemState = new ChatListItemState
            {
                Contact = contact,
                ChatId = chatId,
                LastMessageText = GetLastMessageText(chatId),
                LastMessageSender = GetLastMessageSender(chatId, contact, suppressGroupSenderName),
                LastMessageTime = GetLastMessageTime(chatId),
                HasUnread = _runtimeState.ChatsWithUnreadMessages.Contains(chatId),
                IsOnline = isOnline,
                IsActive = isActive
            };

            return true;
        }

        private string? GetLastMessageText(int chatId)
        {
            if (!_runtimeState.ChatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                return null;
            }

            if (lastMessage.Type == 1)
            {
                return "Voice Message";
            }

            if (lastMessage.Type == 3)
            {
                return "Photo";
            }

            if (lastMessage.Type == 4 || lastMessage.Type == 5)
            {
                return "File";
            }

            return lastMessage.Text;
        }

        private string? GetLastMessageSender(int chatId, Contact contact, bool suppressGroupSenderName)
        {
            if (!_runtimeState.ChatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                return null;
            }

            if (lastMessage.SenderId == _currentUserId)
            {
                return "You";
            }

            if (suppressGroupSenderName &&
                _runtimeState.ChatTypes.TryGetValue(chatId, out var chatType) &&
                chatType != 0)
            {
                return null;
            }

            return contact.Name;
        }

        private DateTime? GetLastMessageTime(int chatId)
        {
            if (_runtimeState.ChatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                return lastMessage.SentAt;
            }

            if (_runtimeState.ChatLastMessageTime.TryGetValue(chatId, out var time))
            {
                return time;
            }

            return null;
        }
    }
}
