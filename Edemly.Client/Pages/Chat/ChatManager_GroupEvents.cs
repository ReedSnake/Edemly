#nullable enable
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Edemly.Client.DTOs;
using Edemly.Client.Models;
using Edemly.Client.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Edemly.Client
{
    public partial class ChatManager
    {
        #region SignalR Group Event Handlers

        private async void OnGroupCreated(int chatId)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (_chatToUserMap.ContainsKey(chatId))
                    {
                        return;
                    }

                    var chats = await _apiService.GetMyChatsAsync();
                    var newChat = chats.FirstOrDefault(c => c.Id == chatId);

                    if (newChat != null)
                    {
                        _chatTypes[newChat.Id] = newChat.Type;
                        if (newChat.LastMessageTime.HasValue)
                        {
                            _chatLastMessageTime[newChat.Id] = newChat.LastMessageTime.Value;
                        }

                        if (newChat.Type == 0)
                        {
                        }
                        else
                        {
                            await LoadAndAddGroupChatAsync(newChat);

                            SortAllChats();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] OnGroupCreated failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Обробник оновлення групи (назва, опис, іконка)
        /// </summary>
        private async void OnGroupUpdated(int chatId, string? name, string? description, string? iconUrl)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Group updated: chatId={chatId}, name={name}, iconUrl={iconUrl}");

                    // Перевіряємо чи це групова контакт
                    if (!_groupContacts.TryGetValue(chatId, out var contact))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Group contact {chatId} not found locally");
                        return;
                    }

                    bool needsUiUpdate = false;

                    // Оновлюємо назву якщо надано
                    if (!string.IsNullOrEmpty(name) && contact.Name != name)
                    {
                        contact.Name = name;
                        needsUiUpdate = true;
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated group name to: {name}");
                    }

                    // Оновлюємо іконку якщо надано
                    if (!string.IsNullOrEmpty(iconUrl) && contact.PhotoPath != iconUrl)
                    {
                        // Інвалідуємо старий кеш
                        if (!string.IsNullOrEmpty(contact.PhotoPath))
                        {
                            try
                            {
                                App.GlobalProfilePictureCache.InvalidateCache(contact.PhotoPath);
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] InvalidateCache failed: {ex}"); }
                        }

                        contact.PhotoPath = iconUrl;
                        needsUiUpdate = true;

                        // Примусово завантажуємо нове зображення
                        try
                        {
                            await App.GlobalProfilePictureCache.ForceDownloadAsync(iconUrl);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to download new icon: {ex.Message}");
                        }

                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated group icon to: {iconUrl}");
                    }

                    // Оновлюємо UI якщо були зміни
                    if (needsUiUpdate)
                    {
                        // Оновлюємо кнопку чату в списку
                        UpdateChatButton(chatId);

                        // Якщо це поточний відкритий чат - оновлюємо заголовок
                        if (CurrentChatId == chatId && CurrentChatContact != null)
                        {
                            if (!string.IsNullOrEmpty(name))
                            {
                                CurrentChatContact.Name = name;
                                _chatHeaderText.Text = name;
                            }
                            if (!string.IsNullOrEmpty(iconUrl))
                            {
                                CurrentChatContact.PhotoPath = iconUrl;
                            }
                            _updateChatHeaderCallback?.Invoke(CurrentChatContact);
                        }

                        System.Diagnostics.Debug.WriteLine("[CHAT MANAGER] Group update processed, UI refreshed");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Error processing group update: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// ✅ НОВИЙ ОБРОБНИК: Оновлення профілю користувача
        /// </summary>
        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Profile updated for user {userId}: {newPfpUrl}");

                    // Find contact for this user
                    if (!_contacts.TryGetValue(userId, out var contact))
                    {
                        // Contact not found locally — try to create it from API if we have a chat mapping
                        var relatedChatIds = _chatToUserMap.Where(kv => kv.Value == userId).Select(kv => kv.Key).ToList();

                        if (relatedChatIds.Count > 0)
                        {
                            try
                            {
                                var user = await _apiService.GetUserByIdAsync(userId);
                                if (user != null)
                                {
                                    var photoPath = string.IsNullOrEmpty(user.PfpUrl) ? DEFAULT_AVATAR_PATH : user.PfpUrl;
                                    contact = new Models.Contact(
                                        user.Id,
                                        user.Username,
                                        user.Email ?? string.Empty,
                                        user.PhoneNumber ?? string.Empty,
                                        photoPath);

                                    lock (_contacts)
                                    {
                                        _contacts[user.Id] = contact;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to load user {userId} from API: {ex.Message}");
                            }
                        }
                    }

                    if (contact != null)
                    {
                        var oldUrl = contact.PhotoPath;

                        // Invalidate old cache (if exists)
                        if (!string.IsNullOrEmpty(oldUrl) && oldUrl != newPfpUrl)
                        {
                            try
                            {
                                App.GlobalProfilePictureCache.InvalidateCache(oldUrl);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to invalidate old cache: {ex.Message}");
                            }
                        }

                        // Force download new picture and update cache (best-effort)
                        BitmapImage? bmp = null;
                        if (!string.IsNullOrEmpty(newPfpUrl))
                        {
                            try
                            {
                                bmp = await App.GlobalProfilePictureCache.ForceDownloadAsync(newPfpUrl);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Force download failed: {ex.Message}");
                            }
                        }

                        // Update contact model
                        contact.PhotoPath = newPfpUrl;

                        // Find all chatIds for this user (there may be more than one)
                        var chatIds = _chatToUserMap.Where(x => x.Value == userId).Select(x => x.Key).ToList();

                        foreach (var chatId in chatIds)
                        {
                            // Rebuild button so the new avatar loads (CreateChatButton will call cache)
                            try
                            {
                                UpdateChatButton(chatId);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to update chat button {chatId}: {ex.Message}");
                            }

                            // If this is the currently open chat, update header
                            if (CurrentChatId == chatId && CurrentChatContact?.UserId == userId)
                            {
                                CurrentChatContact.PhotoPath = newPfpUrl;
                                _updateChatHeaderCallback?.Invoke(CurrentChatContact);
                            }

                            // If we have a freshly downloaded bitmap, also apply it directly to the existing chat button(s)
                            if (bmp != null)
                            {
                                try
                                {
                                    var chatButton = _chatsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is int id && id == chatId);
                                    if (chatButton != null)
                                    {
                                        // The button content is a Grid created in ChatUIBuilder. We try to find the avatar Border and its ImageBrush
                                        if (chatButton.Content is Grid grid)
                                        {
                                            // avatar container expected in column 0
                                            foreach (var child in grid.Children)
                                            {
                                                if (child is Grid avatarContainer)
                                                {
                                                    var avatarBorder = avatarContainer.Children.OfType<Border>().FirstOrDefault();
                                                    if (avatarBorder != null && avatarBorder.Background is ImageBrush ib)
                                                    {
                                                        ib.ImageSource = bmp;
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to apply bitmap directly to chat button {chatId}: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // If contact not known yet, try to refresh caches later or when chat is opened
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Contact {userId} not found in local contacts and no related chats to create one from");

                        // Still attempt to invalidate any cache keyed by the new URL (in case server replaced content at same URL)
                        if (!string.IsNullOrEmpty(newPfpUrl))
                        {
                            try
                            {
                                await App.GlobalProfilePictureCache.ForceDownloadAsync(newPfpUrl);
                            }
                            catch { }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("[CHAT MANAGER] Profile update processed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Error processing profile update: {ex.Message}");
                }
            });
        }

        private void OnHubUserStatusChanged(int userId, bool isOnline, DateTime? lastSeen)
        {
            try
            {
                UpdateStatusCache(userId, isOnline, lastSeen);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Find all chats for this user and update buttons
                    var chatIds = _chatToUserMap.Where(kv => kv.Value == userId).Select(kv => kv.Key).ToList();
                    foreach (var chatId in chatIds)
                    {
                        UpdateChatButtonOnline(chatId, isOnline);
                    }

                    // Также обновите заголовок, если текущий контакт
                    if (CurrentChatContact != null && CurrentChatContact.UserId == userId)
                    {
                        _updateChatHeaderCallback?.Invoke(CurrentChatContact);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] OnHubUserStatusChanged error: {ex.Message}");
            }
        }

        #endregion
    }
}
