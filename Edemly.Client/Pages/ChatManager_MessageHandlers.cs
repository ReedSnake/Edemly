#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Edemly.Client.DTOs;
using Edemly.Client.Helpers;
using Edemly.Client.Models;
using Edemly.Client.Pages;
using Edemly.Client.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Documents;

namespace Edemly.Client
{
    public partial class ChatManager
    {
        #region SignalR Message Event Handlers

        private void OnMessageReceived(MessageDto message)
        {
            Application.Current.Dispatcher.Invoke(async () =>
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
                    var chats = await _apiService.GetMyChatsAsync();
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
                    MakeTextLinksClickable(FindBorderByMessageId(message.Id), message.Text);
                    _messagesScrollViewer.ScrollToEnd();
                }
            });
        }

        private void OnMessageUpdated(MessageDto message)
        {
            Application.Current.Dispatcher.Invoke(() =>
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
                    var messageToUpdate = FindBorderByMessageId(message.Id);

                    if (messageToUpdate != null)
                    {
                        var textBlock = FindMessageTextBlock(messageToUpdate);
                        if (textBlock != null)
                        {
                            var isMyMessage = message.SenderId == CurrentUserId;
                            var newRichText = RichTextHelper.CreateRichTextBlock(
                                message.Text,
                                isMyMessage ? Brushes.Black : Brushes.White,
                                allowSelection: true);

                            newRichText.Margin = textBlock.Margin;

                            var parent = textBlock.Parent as Panel;
                            if (parent != null)
                            {
                                int idx = parent.Children.IndexOf(textBlock);
                                if (idx >= 0)
                                {
                                    parent.Children.RemoveAt(idx);
                                    parent.Children.Insert(idx, newRichText);

                                    var flash = new System.Windows.Media.Animation.DoubleAnimation
                                    {
                                        From = 0.3,
                                        To = 1,
                                        Duration = TimeSpan.FromSeconds(0.3),
                                        AutoReverse = false
                                    };
                                    messageToUpdate.BeginAnimation(UIElement.OpacityProperty, flash);
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Message text block not found for message {message.Id}; skipping UI rebuild to avoid changing layout.");
                        }
                    }
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
            Application.Current.Dispatcher.Invoke(() =>
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

        private void OnConnectionStateChanged(bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
            });
        }

        #endregion

        #region Helper Methods for Finding UI Elements

        /// <summary>
        /// Helper methods for finding message UI elements by message id
        /// </summary>
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

        private TextBlock? FindTextBlockInBorder(Border? border)
        {
            if (border == null) return null;
            return FindTextBlockRecursive(border.Child);
        }

        private TextBlock? FindTextBlockRecursive(object? element)
        {
            if (element == null) return null;

            if (element is TextBlock tb) return tb;

            if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var found = FindTextBlockRecursive(child);
                    if (found != null) return found;
                }
                return null;
            }

            if (element is ContentControl cc)
            {
                return FindTextBlockRecursive(cc.Content);
            }

            if (element is Decorator dec)
            {
                return FindTextBlockRecursive(dec.Child);
            }

            return null;
        }

        /// <summary>
        /// Finds the TextBlock that likely holds the message text inside a message Border.
        /// We try to avoid matching time TextBlocks by checking FontSize and TextWrapping or Cursor.
        /// </summary>
        private TextBlock? FindMessageTextBlock(Border messageBorder)
        {
            if (messageBorder == null) return null;

            TextBlock? candidate = null;

            void Search(object? el)
            {
                if (el == null) return;
                if (candidate != null) return;

                if (el is TextBlock tb)
                {
                    if (tb.Cursor == Cursors.IBeam || tb.TextWrapping == TextWrapping.Wrap || tb.FontSize >= 13)
                    {
                        candidate = tb;
                        return;
                    }
                }

                if (el is Panel p)
                {
                    foreach (var child in p.Children)
                    {
                        Search(child);
                        if (candidate != null) return;
                    }
                }

                if (el is ContentControl cc)
                {
                    Search(cc.Content);
                }

                if (el is Decorator dec)
                {
                    Search(dec.Child);
                }
            }

            Search(messageBorder.Child);
            return candidate;
        }

        #endregion

        #region UI Helper Methods

        private void UpdateChatButton(int chatId)
        {
            var chatButton = _chatsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => b.Tag is int id && id == chatId);

            if (chatButton == null)
            {
                return;
            }

            if (!_chatToUserMap.TryGetValue(chatId, out var userId))
            {
                return;
            }

            Contact? contact;
            if (userId < 0)
            {
                if (!_groupContacts.TryGetValue(chatId, out contact))
                {
                    return;
                }
            }
            else
            {
                if (!_contacts.TryGetValue(userId, out contact))
                {
                    return;
                }
            }

            string? lastMessageText = null;
            string? lastMessageSender = null;
            DateTime? lastMessageTime = null;
            bool hasUnread = _chatsWithUnreadMessages.Contains(chatId);

            if (_chatLastMessage.TryGetValue(chatId, out var lastMessage))
            {
                if (lastMessage.Type == 1)
                {
                    lastMessageText = "Voice Message";
                }
                else if (lastMessage.Type == 3)
                {
                    lastMessageText = "Photo";
                }
                else if (lastMessage.Type == 4 || lastMessage.Type == 5)
                {
                    lastMessageText = "File";
                }
                else
                {
                    lastMessageText = lastMessage.Text;
                }

                if (lastMessage.SenderId == CurrentUserId)
                {
                    lastMessageSender = "You";
                }
                else
                {
                    if (_chatTypes.TryGetValue(chatId, out var chatType) && chatType != 0)
                    {
                        lastMessageSender = null;
                    }
                    else
                    {
                        lastMessageSender = contact.Name;
                    }
                }

                lastMessageTime = lastMessage.SentAt;
            }
            else if (_chatLastMessageTime.TryGetValue(chatId, out var time))
            {
                lastMessageTime = time;
            }

            bool isOnline = GetCachedOnlineForChat(chatId);
            bool isActive = chatId == CurrentChatId;

            int index = _chatsPanel.Children.IndexOf(chatButton);
            _chatsPanel.Children.Remove(chatButton);

            var newButton = _uiBuilder.CreateChatButton(
                contact,
                chatId,
                SwitchToChatAsync,
                lastMessageText,
                lastMessageSender,
                hasUnread,
                isOnline,
                isActive,
                lastMessageTime);

            _chatsPanel.Children.Insert(index, newButton);
        }

        private void UpdateChatButtonOnline(int chatId, bool isOnline)
        {
            try
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => UpdateChatButtonOnline(chatId, isOnline));
                    return;
                }

                var chatButton = _chatsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is int id && id == chatId);
                if (chatButton == null) return;

                int index = _chatsPanel.Children.IndexOf(chatButton);

                if (!_chatToUserMap.TryGetValue(chatId, out var userId)) return;

                Contact? contact;
                if (userId < 0)
                {
                    if (!_groupContacts.TryGetValue(chatId, out contact)) return;
                }
                else
                {
                    if (!_contacts.TryGetValue(userId, out contact)) return;
                }

                string? lastMessageText = null;
                string? lastMessageSender = null;
                DateTime? lastMessageTime = null;
                bool hasUnread = _chatsWithUnreadMessages.Contains(chatId);

                if (_chatLastMessage.TryGetValue(chatId, out var lastMessage))
                {
                    if (lastMessage.Type == 1) lastMessageText = "Voice Message";
                    else if (lastMessage.Type == 3) lastMessageText = "Photo";
                    else if (lastMessage.Type == 4 || lastMessage.Type == 5) lastMessageText = "File";
                    else lastMessageText = lastMessage.Text;

                    lastMessageSender = lastMessage.SenderId == CurrentUserId ? "You" : contact.Name;
                    lastMessageTime = lastMessage.SentAt;
                }
                else if (_chatLastMessageTime.TryGetValue(chatId, out var time))
                {
                    lastMessageTime = time;
                }

                var isActive = chatId == CurrentChatId;
                var newButton = _uiBuilder.CreateChatButton(contact, chatId, SwitchToChatAsync, lastMessageText, lastMessageSender, hasUnread, isOnline, isActive, lastMessageTime);

                _chatsPanel.Children.RemoveAt(index);
                _chatsPanel.Children.Insert(index, newButton);
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
                            var status = System.Text.Json.JsonSerializer.Deserialize<DTOs.UserStatusDto>(json, options);
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

        #endregion
    }
}
