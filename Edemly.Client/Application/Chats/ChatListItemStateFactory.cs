#nullable enable

using System;
using Edemly.Client.Models;
using Edemly.Client.Presentation.Controllers.Chats;
using Edemly.Client.Presentation.Rendering.Chats;
using Edemly.Contracts.Calls;
using Edemly.Contracts.Messages;
using System.Text.Json;

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

            if (lastMessage.Type == MessageTypeCodes.Call)
            {
                return GetCallMessageText(lastMessage.Text);
            }

            return lastMessage.Text;
        }

        private string? GetLastMessageSender(int chatId, Contact contact, bool suppressGroupSenderName)
        {
            if (!_runtimeState.ChatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                return null;
            }

            if (lastMessage.Type == MessageTypeCodes.Call)
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

        private static string GetCallMessageText(string? text)
        {
            var payload = TryReadCallPayload(text);
            if (payload == null)
            {
                return "Call";
            }

            var status = NormalizeStatus(payload.Status);
            return status switch
            {
                CallLifecycleStatuses.Active => "Call in progress",
                CallLifecycleStatuses.Pending => "Call started",
                CallLifecycleStatuses.Missed => "Missed call",
                CallLifecycleStatuses.Rejected => "Call rejected",
                _ => payload.DurationSeconds.HasValue
                    ? $"Call ended ({FormatDuration(payload.DurationSeconds.Value)})"
                    : "Call ended"
            };
        }

        private static CallMessagePayloadDto? TryReadCallPayload(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<CallMessagePayloadDto>(
                    text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.Equals(status, CallLifecycleStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Active;
            }

            if (string.Equals(status, CallLifecycleStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Pending;
            }

            if (string.Equals(status, CallLifecycleStatuses.Missed, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Missed;
            }

            if (string.Equals(status, CallLifecycleStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Rejected;
            }

            return CallLifecycleStatuses.Ended;
        }

        private static string FormatDuration(long durationSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"mm\:ss");
        }
    }
}
