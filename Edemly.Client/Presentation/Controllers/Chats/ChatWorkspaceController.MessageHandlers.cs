#nullable enable

using Edemly.Client.Models;
using Edemly.Contracts.Messages;
using Edemly.Contracts.Users;
using System.Windows;
using System.Windows.Controls;
namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        #region SignalR Message Event Handlers

        private void OnMessageReceived(MessageDto message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                _cache.AddMessage(message.ChatId, message);

                if (!_chatHistory.ContainsKey(message.ChatId))
                {
                    _chatHistory[message.ChatId] = new List<MessageDto>();
                }
                _chatHistory[message.ChatId].Add(message);

                _chatLastMessageTime[message.ChatId] = message.SentAt;
                _chatLastMessage[message.ChatId] = message;

                if (message.SenderId != CurrentUserId && message.ChatId != CurrentChatId)
                {
                    _chatsWithUnreadMessages.Add(message.ChatId);
                }

                if (!_chatToUserMap.ContainsKey(message.ChatId))
                {
                    var chats = await _apiClient.Chats.GetMyChatsAsync();
                    var chat = chats.FirstOrDefault(c => c.Id == message.ChatId);

                    if (chat != null)
                    {
                        _chatTypes[chat.Id] = chat.Type;
                        if (chat.LastMessageTime.HasValue)
                        {
                            _chatLastMessageTime[chat.Id] = chat.LastMessageTime.Value;
                        }

                        if (chat.Type == 0)
                        {
                            var members = await _chatLoader.LoadChatMembersBatchAsync(new List<int> { message.ChatId });
                            if (members != null && members.TryGetValue(message.ChatId, out var memberList))
                            {
                                var otherMember = memberList.FirstOrDefault(m => m.UserId != CurrentUserId);
                                if (otherMember != null)
                                {
                                    await EnsureChatExistsAsync(message.ChatId, otherMember.UserId);
                                }
                            }
                        }
                        else
                        {
                            await LoadAndAddGroupChatAsync(chat);
                        }
                    }
                }

                UpdateChatButton(message.ChatId);
                SortChatsAndMoveToTop(message.ChatId);

                if (message.ChatId == CurrentChatId)
                {
                    if (_chatsWithUnreadMessages.Contains(message.ChatId))
                    {
                        _chatsWithUnreadMessages.Remove(message.ChatId);
                        UpdateChatButton(message.ChatId);
                    }

                    AddDateSeparatorIfNeeded(message.ChatId, message.SentAt);

                    string? senderName = null;
                    if (_chatTypes.TryGetValue(message.ChatId, out var chatType) && chatType == 1)
                    {
                        if (message.SenderId != CurrentUserId)
                        {
                            senderName = await GetUserNameAsync(message.SenderId);
                        }
                    }

                    _messageRenderer.RenderMessage(message, isHistorical: false, senderName);
                    _messagesScrollViewer?.ScrollToEnd();
                }
            });
        }

        private void OnMessageUpdated(MessageDto message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _cache.UpdateMessage(message.ChatId, message);

                if (_chatHistory.TryGetValue(message.ChatId, out var messages))
                {
                    var index = messages.FindIndex(m => m.Id == message.Id);
                    if (index >= 0)
                    {
                        messages[index] = message;
                    }
                }

                if (message.ChatId == CurrentChatId)
                {
                    _messageRenderer.UpdateMessageInUI(message);
                }

                if (_chatLastMessage.ContainsKey(message.ChatId) &&
                    _chatLastMessage[message.ChatId].Id == message.Id)
                {
                    _chatLastMessage[message.ChatId] = message;
                    UpdateChatButton(message.ChatId);
                }
            });
        }

        private void OnMessageDeleted(int messageId, int chatId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _cache.RemoveMessage(chatId, messageId);

                if (_chatHistory.TryGetValue(chatId, out var messages))
                {
                    messages.RemoveAll(m => m.Id == messageId);
                }

                if (chatId == CurrentChatId)
                {
                    var messageToRemove = FindBorderByMessageId(messageId);

                    if (messageToRemove != null)
                    {
                        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0.2)
                        };

                        fadeOut.Completed += (s, e) =>
                        {
                            _messagesPanel.Children.Remove(messageToRemove);
                        };

                        messageToRemove.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                }

                if (_chatLastMessage.ContainsKey(chatId) && _chatLastMessage[chatId].Id == messageId)
                {
                    var lastMsg = _chatHistory.ContainsKey(chatId) ? _chatHistory[chatId].OrderByDescending(m => m.SentAt).FirstOrDefault() : null;
                    if (lastMsg != null)
                    {
                        _chatLastMessage[chatId] = lastMsg;
                    }
                    else
                    {
                        _chatLastMessage.Remove(chatId);
                    }
                    UpdateChatButton(chatId);
                }
            });
        }

        #endregion SignalR Message Event Handlers

        #region Helper Methods for Finding UI Elements

        private bool TagMatches(object? tag, int messageId)
        {
            if (tag == null) return false;
            if (tag is int i) return i == messageId;
            if (tag is long l) return l == messageId;
            if (int.TryParse(tag.ToString(), out var parsed)) return parsed == messageId;
            return false;
        }

        private Border? FindBorderByMessageId(int messageId)
        {
            try
            {
                foreach (var child in _messagesPanel.Children)
                {
                    var found = FindBorderRecursive(child, messageId);
                    if (found != null) return found;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] FindBorderByMessageId failed: {ex}"); }
            return null;
        }

        private Border? FindBorderRecursive(object? element, int messageId)
        {
            if (element == null) return null;

            if (element is Border border)
            {
                if (TagMatches(border.Tag, messageId)) return border;

                var inner = border.Child;
                var foundInner = FindBorderRecursive(inner, messageId);
                if (foundInner != null) return foundInner;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var found = FindBorderRecursive(child, messageId);
                    if (found != null) return found;
                }
            }

            return null;
        }

        #endregion Helper Methods for Finding UI Elements

        #region UI Helper Methods

        private void UpdateChatButton(int chatId)
        {
            var chatButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (chatButton == null)
            {
                return;
            }

            if (!_chatListItemStateFactory.TryGetContact(chatId, out var contact) || contact == null)
            {
                return;
            }

            int index = _chatsPanel.Children.IndexOf(chatButton);
            _chatsPanel.Children.Remove(chatButton);

            var replacement = CreateChatListButton(chatId, suppressGroupSenderName: true);
            if (replacement != null)
            {
                _chatsPanel.Children.Insert(index, replacement);
            }
        }

        private void UpdateChatButtonOnline(int chatId, bool isOnline)
        {
            try
            {
                if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateChatButtonOnline(chatId, isOnline));
                    return;
                }

                var chatButton = _chatsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is int id && id == chatId);
                if (chatButton == null) return;

                int index = _chatsPanel.Children.IndexOf(chatButton);

                if (!_chatListItemStateFactory.TryGetContact(chatId, out var contact) || contact == null)
                {
                    return;
                }

                _chatsPanel.Children.RemoveAt(index);

                var replacement = CreateChatListButton(chatId, suppressGroupSenderName: false, isOnlineOverride: isOnline);
                if (replacement != null)
                {
                    _chatsPanel.Children.Insert(index, replacement);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateChatButtonOnline error: {ex.Message}");
            }
        }

        private async Task RefreshAllPrivateChatStatusesAsync()
        {
            try
            {
                var privateChats = _chatToUserMap.Where(kv => kv.Value > 0).ToList();
                foreach (var kv in privateChats)
                {
                    int chatId = kv.Key;
                    int userId = kv.Value;

                    try
                    {
                        if (TryGetCachedStatus(userId, out var cachedOnline, out var cachedLastSeen))
                        {
                            UpdateChatButtonOnline(chatId, cachedOnline);
                            continue;
                        }

                        var statusObj = await _hubService.QueryUserStatusAsync(userId);
                        if (statusObj != null)
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(statusObj);
                            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var status = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);
                            if (status != null)
                            {
                                UpdateStatusCache(userId, status.IsOnline, status.LastSeen);

                                UpdateChatButtonOnline(chatId, status.IsOnline);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Refresh status for user {userId} failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RefreshAllPrivateChatStatusesAsync error: {ex.Message}");
            }
        }

        #endregion UI Helper Methods
    }
}
