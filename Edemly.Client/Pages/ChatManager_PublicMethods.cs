#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Edemly.Client.DTOs;
using Edemly.Client.Helpers;
using Edemly.Client.Models;
using Edemly.Client.Pages;

namespace Edemly.Client
{
    public partial class ChatManager
    {
        #region Public UI Methods

        private void RebuildMessageBorder(Border border, MessageDto message)
        {
            try
            {
                bool isMy = message.SenderId == CurrentUserId;

                var newBorder = new Border { Tag = message.Id };
                StackPanel container = new StackPanel();

                if (!isMy && _chatTypes.TryGetValue(message.ChatId, out var ct) && ct == 1)
                {
                    string senderName = message.SenderId == CurrentUserId ? "You" : (_userNamesCache.ContainsKey(message.SenderId) ? _userNamesCache[message.SenderId] : "Member");
                    if (!string.IsNullOrEmpty(senderName))
                    {
                        TextBlock nameTb = new TextBlock
                        {
                            Text = senderName,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B4DCDC")),
                            Margin = new Thickness(0, 0, 0, 3)
                        };
                        container.Children.Add(nameTb);
                    }
                }

                var messageText = RichTextHelper.CreateRichTextBlock(
                    message.Text,
                    isMy ? Brushes.Black : Brushes.White,
                    allowSelection: true);
                messageText.Margin = new Thickness(0, 0, 0, 5);

                string timeString = message.SentAt.ToLocalTime().ToString("HH:mm");
                TextBlock timeText = new TextBlock
                {
                    Text = timeString,
                    FontSize = 10,
                    Foreground = isMy ? Brushes.Black : Brushes.White,
                    Opacity = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (isMy)
                {
                    StackPanel bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    bottom.Children.Add(timeText);
                    container.Children.Add(messageText);
                    container.Children.Add(bottom);

                    newBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C4FFF8"));
                    newBorder.CornerRadius = new CornerRadius(15, 15, 0, 15);
                    newBorder.Margin = new Thickness(150, 8, 15, 8);
                    newBorder.HorizontalAlignment = HorizontalAlignment.Right;
                }
                else
                {
                    container.Children.Add(messageText);
                    container.Children.Add(timeText);
                    container.Margin = new Thickness(12, 8, 12, 8);

                    newBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#057272"));
                    newBorder.CornerRadius = new CornerRadius(15, 15, 15, 0);
                    newBorder.Margin = new Thickness(15, 8, 150, 8);
                    newBorder.HorizontalAlignment = HorizontalAlignment.Left;
                }

                newBorder.MaxWidth = 500;
                newBorder.Padding = new Thickness(0);
                newBorder.Child = container;
                newBorder.Opacity = 1;

                newBorder.MouseEnter += (s, e) => { timeText.Opacity = 0.7; };
                newBorder.MouseLeave += (s, e) => { timeText.Opacity = 0; };

                try { AddMessageContextMenu(newBorder, message); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] AddMessageContextMenu failed: {ex}"); }

                int idx = _messagesPanel.Children.IndexOf(border);
                if (idx >= 0)
                {
                    _messagesPanel.Children.RemoveAt(idx);
                    _messagesPanel.Children.Insert(idx, newBorder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RebuildMessageBorder error: {ex.Message}");
            }
        }

        public bool IsCurrentChatGroup()
        {
            if (CurrentChatId < 0) return false;
            return _chatTypes.TryGetValue(CurrentChatId, out var t) && t == 1;
        }

        private void AddMessageContextMenu(Border messageBorder, MessageDto message)
        {
            var contextMenu = new ContextMenu();

            if (message.Type == 0 && !string.IsNullOrEmpty(message.Text))
            {
                var copy = new MenuItem { Header = "📝 Copy", FontSize = 13 };
                copy.Click += (s, e) => Clipboard.SetText(message.Text);
                contextMenu.Items.Add(copy);
            }

            if (message.SenderId == CurrentUserId)
            {
                if (contextMenu.Items.Count > 0) contextMenu.Items.Add(new Separator());

                if (message.Type == 0)
                {
                    var edit = new MenuItem { Header = "✎ Edit", FontSize = 13 };
                    edit.Click += async (s, e) => await EditMessageAsync(message);
                    contextMenu.Items.Add(edit);
                }

                var del = new MenuItem { Header = "🗑️ Delete", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)) };
                del.Click += async (s, e) => await DeleteMessageAsync(message);
                contextMenu.Items.Add(del);
            }

            if (contextMenu.Items.Count > 0)
            {
                messageBorder.ContextMenu = contextMenu;
            }
        }

        private async Task EditMessageAsync(MessageDto message)
        {
            try
            {
                var input = new Window
                {
                    Title = "Edit Message",
                    Width = 440,
                    Height = 260,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = Application.Current.MainWindow
                };

                var grid = new Grid { Margin = new Thickness(12) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock { Text = "Edit your message:", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
                Grid.SetRow(label, 0); grid.Children.Add(label);

                var tb = new TextBox { Text = message.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 13, Padding = new Thickness(8) };
                Grid.SetRow(tb, 1); grid.Children.Add(tb);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
                Grid.SetRow(buttons, 2);
                var cancel = new Button { Content = "Cancel", Width = 80, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
                cancel.Click += (s, e) => input.DialogResult = false;
                var save = new Button { Content = "Save", Width = 80, Height = 30, Background = new SolidColorBrush(Color.FromRgb(5, 114, 114)), Foreground = Brushes.White };
                save.Click += (s, e) => input.DialogResult = true;
                buttons.Children.Add(cancel); buttons.Children.Add(save); grid.Children.Add(buttons);

                input.Content = grid;

                if (input.ShowDialog() == true)
                {
                    var newText = tb.Text.Trim();
                    if (string.IsNullOrEmpty(newText))
                    {
                        Edemly.Client.Pages.MessageBox.ShowWarning("Message cannot be empty", "Validation");
                        return;
                    }

                    if (newText == message.Text) return;

                    var updated = new UpdateMessageDto { Id = message.Id, ChatId = message.ChatId, Text = newText };
                    bool success = await App.HubService.UpdateMessageAsync(updated);
                    if (!success) Edemly.Client.Pages.MessageBox.ShowError("Failed to update message", "Error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] EditMessageAsync error: {ex.Message}");
                Edemly.Client.Pages.MessageBox.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private async Task DeleteMessageAsync(MessageDto message)
        {
            try
            {
                var result = Edemly.Client.Pages.MessageBox.ShowQuestion("Are you sure you want to delete this message?", "Confirm Delete");
                if (result == MessageBoxResult.Yes)
                {
                    bool success = await App.HubService.DeleteMessageAsync(message.Id, message.ChatId);
                    if (!success) Edemly.Client.Pages.MessageBox.ShowError("Failed to delete message", "Error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] DeleteMessageAsync error: {ex.Message}");
                Edemly.Client.Pages.MessageBox.ShowError($"Error: {ex.Message}", "Error");
            }
        }

        private void MakeTextLinksClickable(Border? messageBorder, string messageText)
        {
            if (messageBorder == null || string.IsNullOrEmpty(messageText)) return;

            var textBlock = FindMessageTextBlock(messageBorder);
            if (textBlock == null) return;

            if (!(messageText.Contains("http://") || messageText.Contains("https://") ||
                  messageText.Contains("www.") || messageText.Contains("@")))
                return;

            var newTextBlock = new TextBlock
            {
                FontSize = textBlock.FontSize,
                FontFamily = textBlock.FontFamily,
                TextWrapping = TextWrapping.Wrap,
                Margin = textBlock.Margin,
                Foreground = textBlock.Foreground,
                Cursor = Cursors.IBeam
            };

            var parts = SplitTextIntoParts(messageText);

            foreach (var part in parts)
            {
                if (IsLinkOrEmail(part))
                {
                    var hyperlink = new Hyperlink(new Run(part))
                    {
                        Foreground = Brushes.DodgerBlue,
                        TextDecorations = System.Windows.TextDecorations.Underline,
                        Cursor = Cursors.Hand
                    };

                    hyperlink.Click += (s, e) => OpenLink(part);
                    newTextBlock.Inlines.Add(hyperlink);
                }
                else
                {
                    newTextBlock.Inlines.Add(new Run(part));
                }
            }

            if (textBlock.Parent is Panel parent)
            {
                int index = parent.Children.IndexOf(textBlock);
                parent.Children.RemoveAt(index);
                parent.Children.Insert(index, newTextBlock);
            }
        }

        private bool IsLinkOrEmail(string text)
        {
            if (text.Contains("@") && text.Contains(".") && !text.Contains(" "))
                return true;

            if (text.StartsWith("http://") || text.StartsWith("https://") ||
                text.StartsWith("www.") && !text.Contains(" "))
                return true;

            return false;
        }

        private List<string> SplitTextIntoParts(string text)
        {
            var parts = new List<string>();
            var words = text.Split(' ');

            foreach (var word in words)
            {
                if (IsLinkOrEmail(word))
                {
                    parts.Add(word);
                }
                else
                {
                    if (parts.Count > 0 && !IsLinkOrEmail(parts.Last()))
                    {
                        parts[parts.Count - 1] += " " + word;
                    }
                    else
                    {
                        parts.Add(word);
                    }
                }
            }

            return parts;
        }

        private void OpenLink(string url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo();

                if (url.Contains("@"))
                {
                    psi.FileName = $"mailto:{url}";
                }
                else
                {
                    if (url.StartsWith("www."))
                        url = "http://" + url;
                    psi.FileName = url;
                }

                psi.UseShellExecute = true;
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] OpenLink failed: {ex}"); }
        }

        public void UpdateCurrentChatPhoto(string newPhotoPath)
        {
            try
            {
                if (CurrentChatContact == null || CurrentChatId < 0) return;

                CurrentChatContact.PhotoPath = newPhotoPath;
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated photo for chat {CurrentChatId}: {newPhotoPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Error updating photo: {ex.Message}");
            }
        }

        /// <summary>
        /// Оновлює іконку групи та перебудовує кнопку чату
        /// </summary>
        public void UpdateGroupIcon(int chatId, string newIconUrl)
        {
            try
            {
                if (!_groupContacts.TryGetValue(chatId, out var contact))
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateGroupIcon: contact not found for chatId {chatId}");
                    return;
                }

                if (!string.IsNullOrEmpty(contact.PhotoPath))
                {
                    try
                    {
                        App.GlobalProfilePictureCache.InvalidateCache(contact.PhotoPath);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] InvalidateCache failed: {ex}"); }
                }

                contact.PhotoPath = newIconUrl;

                UpdateChatButton(chatId);

                if (CurrentChatId == chatId && CurrentChatContact != null)
                {
                    CurrentChatContact.PhotoPath = newIconUrl;
                    _updateChatHeaderCallback?.Invoke(CurrentChatContact);
                }

                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated group icon for chat {chatId}: {newIconUrl}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateGroupIcon error: {ex.Message}");
            }
        }

        #endregion
    }
}
