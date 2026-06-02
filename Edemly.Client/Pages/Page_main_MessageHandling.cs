#nullable disable
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = Edemly.Client.Pages.MessageBox;
using Edemly.Client.DTOs;
using Edemly.Client.Helpers;
using Edemly.Client.Lang;

namespace Edemly.Client
{
    public partial class Page_main : Page
    {
        private bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.Trim();

            // Include all placeholder variants used in UI/locales
            return text == DefaultLanguage.TypeMessage
                || text == DefaultLanguage.Loading
                || text == "Message..."
                || text == "Type a message..."
                || text == "Введите сообщение..."
                || text == "Введіть повідомлення...";
        }

        private void SetMessagePlaceholder()
        {
            if (MessageTextBox == null)
                return;

            MessageTextBox.Text = DefaultLanguage.TypeMessage;
            MessageTextBox.Foreground = Brushes.Gray;
            MessageTextBox.FontStyle = FontStyles.Italic;
        }

        private void RestoreMessageInputText(string text)
        {
            if (MessageTextBox == null)
                return;

            if (string.IsNullOrWhiteSpace(text) || IsPlaceholderText(text))
            {
                SetMessagePlaceholder();
                return;
            }

            MessageTextBox.Text = text;
            MessageTextBox.Foreground = Brushes.Black;
            MessageTextBox.FontStyle = FontStyles.Normal;
        }

        private void ResetSendButtonForCurrentMessageInput()
        {
            if (SendButton == null || MessageTextBox == null)
                return;

            SendButton.IsEnabled = true;
            SendButton.Background = Brushes.Transparent;
            SendButton.ToolTip = null;

            if (IsPlaceholderText(MessageTextBox.Text))
            {
                SendButton.Content = "🎤";
                SendButton.Tag = "voice";
            }
            else
            {
                SendButton.Content = "➤";
                SendButton.Tag = "send";
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var tag = SendButton?.Tag?.ToString();

            if (tag == "voice")
            {
                await HandleVoiceRecordingAsync();
                return;
            }

            if (tag == "recording" || _isRecording)
            {
                await HandleVoiceRecordingAsync();
                return;
            }

            string message = MessageTextBox.Text.Trim();

            if (!string.IsNullOrEmpty(message) && !IsPlaceholderText(message))
            {
                if (chatManager.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat via search", "Error");
                    return;
                }

                await chatManager.SendMessageAsync(message);
                SetMessagePlaceholder();
                MessageTextBox.Focus();
            }
        }

        private async void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_isRecording)
                {
                    e.Handled = true;
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    return;
                }

                e.Handled = true;

                if (chatManager.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning("First select a contact to chat", "Error");
                    return;
                }

                string message = MessageTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(message) && !IsPlaceholderText(message))
                {
                    await chatManager.SendMessageAsync(message);
                    SetMessagePlaceholder();
                }
            }
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isRecording)
                return;

            if (SendButton == null)
                return;

            if (IsPlaceholderText(MessageTextBox.Text))
            {
                ResetSendButtonForCurrentMessageInput();
            }
            else
            {
                ResetSendButtonForCurrentMessageInput();
            }
        }

        private void OnUserStatusChanged(int userId, bool isOnline, DateTime? lastSeen)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (chatManager?.CurrentChatContact != null &&
                    chatManager.CurrentChatContact.UserId == userId)
                {
                    var isGroup = chatManager.IsCurrentChatGroup();
                    if (!isGroup)
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
                onlineIndicator.Fill = Brushes.LimeGreen;
                statusText.Text = "Online";
                statusText.Foreground = Brushes.LimeGreen;
            }
            else
            {
                onlineIndicator.Fill = Brushes.Gray;

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

                statusText.Foreground = Brushes.Gray;
            }

            System.Diagnostics.Debug.WriteLine($"[STATUS] Updated: {(isOnline ? "Online" : statusText.Text)}");
        }

        public async void UpdateChatHeader(Models.Contact contact)
        {
            System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] UpdateChatHeader called with contact: {contact?.Name ?? "null"}");

            var onlinePanel = this.FindName("OnlineStatusPanel") as StackPanel;

            if (contact == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Hiding header - no contact");
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

            ChatHeaderText.Text = contact.Name;
            ChatHeaderText.Margin = new Thickness(0);
            ChatHeaderAvatarBorder.Visibility = Visibility.Visible;
            ChatHeaderText.Visibility = Visibility.Visible;

            if (onlinePanel != null)
            {
                var isGroupChat = chatManager?.IsCurrentChatGroup() ?? false;

                if (!isGroupChat)
                {
                    onlinePanel.Visibility = Visibility.Visible;

                    if (chatManager != null &&
                        chatManager.TryGetCachedUserStatus(contact.UserId, out var cachedOnline, out var cachedLastSeen))
                    {
                        UpdateOnlineStatus(cachedOnline, cachedLastSeen);
                    }
                    else
                    {
                        UpdateOnlineStatus(false, null);

                        try
                        {
                            var status = await chatManager.RefreshUserStatusAsync(contact.UserId);
                            if (status.Found &&
                                chatManager?.CurrentChatContact != null &&
                                chatManager.CurrentChatContact.UserId == contact.UserId &&
                                !(chatManager.IsCurrentChatGroup()))
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

            try
            {
                if (string.IsNullOrEmpty(contact.PhotoPath) ||
                    contact.PhotoPath == "pack://application:,,,/Assets/avatar.png")
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Using default avatar");
                    ChatHeaderAvatarBackground.ImageSource = new BitmapImage(
                        new Uri("pack://application:,,,/Assets/avatar.png", UriKind.RelativeOrAbsolute));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Loading from cache: {contact.PhotoPath}");
                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(contact.PhotoPath);

                    if (bitmap != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Avatar loaded successfully");
                        ChatHeaderAvatarBackground.ImageSource = bitmap;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Failed to load, using default");
                        ChatHeaderAvatarBackground.ImageSource = new BitmapImage(
                            new Uri("pack://application:,,,/Assets/avatar.png", UriKind.RelativeOrAbsolute));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT HEADER] Error loading avatar: {ex.Message}");
                ChatHeaderAvatarBackground.ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Assets/avatar.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}
