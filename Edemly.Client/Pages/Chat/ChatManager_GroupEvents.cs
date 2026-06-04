#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        private async void OnGroupUpdated(int chatId, string? name, string? description, string? iconUrl)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Group updated: chatId={chatId}, name={name}, iconUrl={iconUrl}");

                    if (!_groupContacts.TryGetValue(chatId, out var contact))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Group contact {chatId} not found locally");
                        return;
                    }

                    bool needsUiUpdate = false;

                    if (!string.IsNullOrEmpty(name) && contact.Name != name)
                    {
                        contact.Name = name;
                        needsUiUpdate = true;
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated group name to: {name}");
                    }

                    if (!string.IsNullOrEmpty(iconUrl) && contact.PhotoPath != iconUrl)
                    {
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

                    if (needsUiUpdate)
                    {
                        UpdateChatButton(chatId);

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

        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Profile updated for user {userId}: {newPfpUrl}");

                    if (!_contacts.TryGetValue(userId, out var contact))
                    {
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

                        contact.PhotoPath = newPfpUrl;

                        var chatIds = _chatToUserMap.Where(x => x.Value == userId).Select(x => x.Key).ToList();

                        foreach (var chatId in chatIds)
                        {
                            try
                            {
                                UpdateChatButton(chatId);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Failed to update chat button {chatId}: {ex.Message}");
                            }

                            if (CurrentChatId == chatId && CurrentChatContact?.UserId == userId)
                            {
                                CurrentChatContact.PhotoPath = newPfpUrl;
                                _updateChatHeaderCallback?.Invoke(CurrentChatContact);
                            }

                            if (bmp != null)
                            {
                                try
                                {
                                    var chatButton = _chatsPanel.Children.OfType<Button>().FirstOrDefault(b => b.Tag is int id && id == chatId);
                                    if (chatButton != null)
                                    {
                                        if (chatButton.Content is Grid grid)
                                        {
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
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Contact {userId} not found in local contacts and no related chats to create one from");

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
                    var chatIds = _chatToUserMap.Where(kv => kv.Value == userId).Select(kv => kv.Key).ToList();
                    foreach (var chatId in chatIds)
                    {
                        UpdateChatButtonOnline(chatId, isOnline);
                    }

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

        #endregion SignalR Group Event Handlers
    }
}