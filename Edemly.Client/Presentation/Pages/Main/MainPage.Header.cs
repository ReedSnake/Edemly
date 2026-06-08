#nullable disable

using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private void OnUserStatusChanged(int userId, bool isOnline, DateTime? lastSeen)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_chatController?.CurrentChatContact != null &&
                    _chatController.CurrentChatContact.UserId == userId)
                {
                    if (!_chatController.IsCurrentChatGroup())
                    {
                        UpdateOnlineStatus(isOnline, lastSeen);
                    }
                    else
                    {
                        var onlinePanel = this.FindName("OnlineStatusPanel") as StackPanel;
                        if (onlinePanel != null)
                        {
                            onlinePanel.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            });
        }

        private void UpdateOnlineStatus(bool isOnline, DateTime? lastSeen)
        {
            var onlineIndicator = this.FindName("OnlineIndicator") as System.Windows.Shapes.Ellipse;
            var statusText = this.FindName("StatusText") as TextBlock;

            if (onlineIndicator == null || statusText == null)
            {
                System.Diagnostics.Debug.WriteLine("[STATUS] Status elements not found in XAML");
                return;
            }

            if (isOnline)
            {
                SetThemeResource(onlineIndicator, System.Windows.Shapes.Shape.FillProperty, "ThemeOnlineBrush");
                statusText.Text = "Online";
                SetThemeResource(statusText, TextBlock.ForegroundProperty, "ThemeOnlineBrush");
            }
            else
            {
                SetThemeResource(onlineIndicator, System.Windows.Shapes.Shape.FillProperty, "ThemeDisabledTextBrush");

                if (lastSeen.HasValue)
                {
                    var timeAgo = DateTime.UtcNow - lastSeen.Value;

                    if (timeAgo.TotalMinutes < 1)
                        statusText.Text = "Just now";
                    else if (timeAgo.TotalMinutes < 60)
                        statusText.Text = $"{(int)timeAgo.TotalMinutes}m ago";
                    else if (timeAgo.TotalHours < 24)
                        statusText.Text = $"{(int)timeAgo.TotalHours}h ago";
                    else
                        statusText.Text = $"{(int)timeAgo.TotalDays}d ago";
                }
                else
                {
                    statusText.Text = "Offline";
                }

                SetThemeResource(statusText, TextBlock.ForegroundProperty, "ThemeDisabledTextBrush");
            }

            System.Diagnostics.Debug.WriteLine($"[STATUS] Updated: {(isOnline ? "Online" : statusText.Text)}");
        }

        public async void UpdateChatHeader(Models.Contact contact)
        {
            System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] UpdateChatHeader called with contact: {contact?.Name ?? "null"}");

            var onlinePanel = this.FindName("OnlineStatusPanel") as StackPanel;

            if (contact == null)
            {
                System.Diagnostics.Debug.WriteLine("[CHAT HEADER] Hiding header - no contact");
                ChatHeaderAvatarBorder.Visibility = Visibility.Collapsed;
                ChatHeaderText.Visibility = Visibility.Collapsed;
                ChatHeaderText.Text = "";

                if (onlinePanel != null)
                {
                    onlinePanel.Visibility = Visibility.Collapsed;
                }

                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Showing header for: {contact.Name}, path: {contact.PhotoPath}");

            ChatHeaderText.Text = contact.DisplayName ?? contact.Name;
            ChatHeaderText.Margin = new Thickness(0);
            ChatHeaderAvatarBorder.Visibility = Visibility.Visible;
            ChatHeaderText.Visibility = Visibility.Visible;

            if (onlinePanel != null)
            {
                var isGroupChat = _chatController?.IsCurrentChatGroup() ?? false;

                if (!isGroupChat)
                {
                    onlinePanel.Visibility = Visibility.Visible;

                    if (_chatController != null &&
                        _chatController.TryGetCachedUserStatus(contact.UserId, out var cachedOnline, out var cachedLastSeen))
                    {
                        UpdateOnlineStatus(cachedOnline, cachedLastSeen);
                    }
                    else
                    {
                        UpdateOnlineStatus(false, null);

                        try
                        {
                            var status = await _chatController.RefreshUserStatusAsync(contact.UserId);
                            if (status.Found &&
                                _chatController?.CurrentChatContact != null &&
                                _chatController.CurrentChatContact.UserId == contact.UserId &&
                                !_chatController.IsCurrentChatGroup())
                            {
                                UpdateOnlineStatus(status.IsOnline, status.LastSeenUtc);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Failed to refresh status: {ex.Message}");
                        }
                    }
                }
                else
                {
                    onlinePanel.Visibility = Visibility.Collapsed;
                }
            }

            await SetHeaderAvatarAsync(contact.PhotoPath);
        }

        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { });
                await ProcessProfileUpdatedAsync(userId, newPfpUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Error processing profile update: {ex.Message}");
            }
        }

        private async Task ProcessProfileUpdatedAsync(int userId, string newPfpUrl)
        {
            System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Profile updated for user {userId}: {newPfpUrl}");
            var normalizedPhotoPath = string.IsNullOrWhiteSpace(newPfpUrl)
                ? Models.Contact.DefaultAvatarPath
                : newPfpUrl;

            if (_chatController?.CurrentChatContact != null &&
                _chatController.CurrentChatContact.UserId == userId)
            {
                _chatController.CurrentChatContact.PhotoPath = normalizedPhotoPath;
            }

            try
            {
                var cache = App.GlobalProfilePictureCache;
                if (cache != null)
                {
                    string oldUrl = null;

                    if (_chatController?.CurrentChatContact != null && _chatController.CurrentChatContact.UserId == userId)
                    {
                        oldUrl = _chatController.CurrentChatContact.PhotoPath;
                    }

                    if (!string.IsNullOrEmpty(oldUrl) && oldUrl != newPfpUrl)
                    {
                        try { cache.InvalidateCache(oldUrl); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] InvalidateCache failed: {ex}"); }
                    }

                    if (!string.IsNullOrEmpty(newPfpUrl))
                    {
                        try { await cache.ForceDownloadAsync(newPfpUrl); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] ForceDownloadAsync failed: {ex}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Prefetch failed: {ex.Message}");
            }

            if (isContactInfoOpen &&
                _chatController?.CurrentChatContact != null &&
                _chatController.CurrentChatContact.UserId == userId)
            {
                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Updating Contact Info photo");
                await MainPageAvatarHelper.SetImageSourceAsync(ContactPhotoBackground, normalizedPhotoPath, "[PAGE_MAIN] Contact info");
            }

            if (_chatController?.CurrentChatContact != null &&
                _chatController.CurrentChatContact.UserId == userId)
            {
                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Updating chat header photo");
                await SetHeaderAvatarAsync(normalizedPhotoPath);
            }

            System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Profile update processed in Page_main");
        }

        private async Task SetHeaderAvatarAsync(string photoPath)
        {
            await MainPageAvatarHelper.SetImageSourceAsync(ChatHeaderAvatarBackground, photoPath, "[CHAT HEADER]");
        }
    }
}
