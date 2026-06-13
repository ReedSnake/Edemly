#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        private void RequestSortAllChatsDebounced()
        {
            try
            {
                lock (_sortLock)
                {
                    _sortDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _sortDebounceTimer = new System.Threading.Timer(_ =>
                    {
                        try { System.Windows.Application.Current?.Dispatcher?.Invoke(() => SortAllChats()); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] SortAllChats invoke failed: {ex}"); }
                    }, null, _sortDebouncePeriod, Timeout.InfiniteTimeSpan);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RequestSortAllChatsDebounced failed: {ex}"); }
        }

        private void UpdateChatButtonIfExists(int chatId)
        {
            var chatButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (chatButton != null)
            {
                UpdateChatButton(chatId);
            }
        }

        private Button? CreateChatListButton(
            int chatId,
            bool suppressGroupSenderName,
            bool? isOnlineOverride = null)
        {
            var isActive = chatId == CurrentChatId;
            var isOnline = isOnlineOverride ?? GetCachedOnlineForChat(chatId);

            if (!_chatListItemStateFactory.TryCreate(chatId, suppressGroupSenderName, isOnline, isActive, out var itemState) ||
                itemState == null)
            {
                return null;
            }

            return _chatListItemBuilder.CreateChatButton(itemState, SwitchToChatAsync);
        }

        private void SortAllChats()
        {
            var sortedChatIds = _chatToUserMap.Keys
                .OrderByDescending(chatId => _chatListItemStateFactory.GetLastActivity(chatId))
                .ThenBy(chatId => chatId)
                .ToList();

            _chatsPanel.Children.Clear();

            foreach (var chatId in sortedChatIds)
            {
                var chatButton = CreateChatListButton(chatId, suppressGroupSenderName: true);
                if (chatButton == null)
                {
                    continue;
                }

                _chatsPanel.Children.Add(chatButton);
            }
        }

        private void SortChatsAndMoveToTop(int chatId)
        {
            if (!_chatToUserMap.ContainsKey(chatId))
            {
                return;
            }

            RequestSortAllChatsDebounced();
        }

        private void AddChatToList(int chatId)
        {
            var existingButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (existingButton != null)
            {
                return;
            }

            var chatButton = CreateChatListButton(chatId, suppressGroupSenderName: false);
            if (chatButton != null)
            {
                _chatsPanel.Children.Add(chatButton);
            }
        }
    }
}
