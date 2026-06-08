#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Audio;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupNameTextBox == null || ParticipantsPanel == null || ParticipantCountText == null)
            {
                System.Diagnostics.Debug.WriteLine("Dialog elements are not initialized");
                return;
            }

            GroupNameTextBox.Text = string.Empty;
            ParticipantsPanel.Children.Clear();
            _selectedParticipants.Clear();
            UpdateParticipantCountText();
            ResetParticipantSearchState();

            CreateGroupOverlay.Visibility = Visibility.Visible;
            CreateGroupDialog.Visibility = Visibility.Visible;
        }

        private void CancelCreateGroup_Click(object sender, RoutedEventArgs e)
        {
            CloseCreateGroupDialog();
        }

        private void CreateGroupOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseCreateGroupDialog();
        }

        private void CloseCreateGroupDialog()
        {
            if (CreateGroupOverlay == null || CreateGroupDialog == null) return;

            CreateGroupOverlay.Visibility = Visibility.Collapsed;
            CreateGroupDialog.Visibility = Visibility.Collapsed;

            if (GroupNameTextBox != null) GroupNameTextBox.Text = string.Empty;
            if (ParticipantsPanel != null) ParticipantsPanel.Children.Clear();

            _selectedParticipants.Clear();
            ResetParticipantSearchState();
        }

        private void ResetParticipantSearchState()
        {
            if (ParticipantSearchTextBox == null)
            {
                return;
            }

            ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
            ApplyParticipantSearchTextBoxLayout();
        }

        private void ApplyParticipantSearchTextBoxLayout()
        {
            if (ParticipantSearchTextBox == null)
            {
                return;
            }

            ParticipantSearchTextBox.TextAlignment = TextAlignment.Left;
            ParticipantSearchTextBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            ParticipantSearchTextBox.VerticalContentAlignment = VerticalAlignment.Center;

            if (GroupNameTextBox != null && !double.IsNaN(GroupNameTextBox.ActualHeight) && GroupNameTextBox.ActualHeight > 0)
            {
                ParticipantSearchTextBox.MinHeight = GroupNameTextBox.ActualHeight;
                ParticipantSearchTextBox.Height = GroupNameTextBox.ActualHeight;
            }
            else
            {
                ParticipantSearchTextBox.MinHeight = 40;
                ParticipantSearchTextBox.Height = 40;
            }
        }

        private async void ConfirmCreateGroup_Click(object sender, RoutedEventArgs e)
        {
            string groupName = GroupNameTextBox?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.ShowWarning(DefaultLanguage.GroupNameEmpty, DefaultLanguage.Validation);
                return;
            }

            if (_selectedParticipants.Count == 0)
            {
                MessageBox.ShowWarning(DefaultLanguage.SelectMembers, DefaultLanguage.Validation);
                return;
            }

            try
            {
                if (ConfirmCreateGroupButton != null)
                {
                    ConfirmCreateGroupButton.IsEnabled = false;
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP] Creating group '{groupName}' with {_selectedParticipants.Count} participants");

                var participantsList = _selectedParticipants.ToList();

                System.Diagnostics.Debug.WriteLine($"[GROUP] Sending to API: {string.Join(", ", participantsList)}");

                var groupChat = await App.ApiClients.Chats.CreateGroupChatAsync(groupName, participantsList);

                if (groupChat == null)
                {
                    MessageBox.ShowError(DefaultLanguage.FailedCreateGroup, DefaultLanguage.ErrorTitle);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP] Group created successfully: ID={groupChat.Id}, Name={groupChat.Name}");

                CloseCreateGroupDialog();

                var photoPath = string.IsNullOrEmpty(groupChat.IconUrl)
                    ? Models.Contact.DefaultAvatarPath
                    : groupChat.IconUrl;

                var groupContact = Models.Contact.CreateGroup(groupChat.Id, groupChat.Name, photoPath);

                System.Diagnostics.Debug.WriteLine($"[GROUP] Created contact for group: {groupContact.Name}");

                await _chatController.AddGroupChatAndSwitchAsync(groupContact, groupChat.Id);

                System.Diagnostics.Debug.WriteLine($"[GROUP] Switched to group chat {groupChat.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP] Error creating group: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GROUP] Stack trace: {ex.StackTrace}");
                MessageBox.ShowError($"{DefaultLanguage.FailedCreateGroup}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
            finally
            {
                if (ConfirmCreateGroupButton != null)
                {
                    ConfirmCreateGroupButton.IsEnabled = true;
                }
            }
        }

        private void ParticipantSearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ParticipantSearchTextBox == null) return;

                if (ParticipantSearchTextBox.Text == DefaultLanguage.SearchUsers)
                {
                    ApplyTextInputActiveStyle(ParticipantSearchTextBox, string.Empty);
                }

                ApplyParticipantSearchTextBoxLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP] ParticipantSearchTextBox_GotFocus error: {ex.Message}");
            }
        }

        private void ParticipantSearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ParticipantSearchTextBox == null) return;

                if (string.IsNullOrWhiteSpace(ParticipantSearchTextBox.Text))
                {
                    ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
                }

                ApplyParticipantSearchTextBoxLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP] ParticipantSearchTextBox_LostFocus error: {ex.Message}");
            }
        }

        private async void ParticipantSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ParticipantsPanel == null)
            {
                System.Diagnostics.Debug.WriteLine("[GROUP] ParticipantsPanel is null");
                return;
            }

            var searchText = ParticipantSearchTextBox.Text;
            System.Diagnostics.Debug.WriteLine($"[GROUP] Search text changed: '{searchText}'");

            if (string.IsNullOrWhiteSpace(searchText) || searchText == DefaultLanguage.SearchUsers)
            {
                ParticipantsPanel.Children.Clear();
                System.Diagnostics.Debug.WriteLine("[GROUP] Cleared participants panel");
                return;
            }

            try
            {
                if (App.ApiClients == null)
                {
                    System.Diagnostics.Debug.WriteLine("[GROUP] _apiClient is not initialized yet");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP] Searching for users: {searchText}");
                var users = await App.ApiClients.Users.SearchUsersAsync(searchText);
                System.Diagnostics.Debug.WriteLine($"[GROUP] Found {users.Count} users");

                ParticipantsPanel.Children.Clear();

                foreach (var user in users)
                {
                    if (user.Id == App.CurrentUserId) continue;

                    System.Diagnostics.Debug.WriteLine($"[GROUP] Adding user: {user.Username}");
                    var userButton = CreateParticipantCheckbox(user);
                    ParticipantsPanel.Children.Add(userButton);
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP] Total added: {ParticipantsPanel.Children.Count} users");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP] Error searching participants: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GROUP] Stack trace: {ex.StackTrace}");
            }
        }

        private Border CreateParticipantCheckbox(UserDto user)
        {
            var container = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(10),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var checkBox = new CheckBox
            {
                IsChecked = _selectedParticipants.Contains(user.Id),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            checkBox.Checked += (s, e) =>
            {
                _selectedParticipants.Add(user.Id);
                UpdateParticipantCountText();
            };

            checkBox.Unchecked += (s, e) =>
            {
                _selectedParticipants.Remove(user.Id);
                UpdateParticipantCountText();
            };

            Grid.SetColumn(checkBox, 0);

            var avatar = new Border
            {
                Width = 35,
                Height = 35,
                CornerRadius = new CornerRadius(17.5),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            SetThemeResource(avatar, Border.BackgroundProperty, "ThemePrimaryLightBrush");

            var avatarText = new TextBlock
            {
                Text = user.Username.Substring(0, Math.Min(2, user.Username.Length)).ToUpper(),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            SetThemeResource(avatarText, TextBlock.ForegroundProperty, "ThemeOnSecondaryTextBrush");

            avatar.Child = avatarText;
            Grid.SetColumn(avatar, 1);

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var username = new TextBlock
            {
                Text = user.Username,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2),
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            SetThemeResource(username, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
            textPanel.Children.Add(username);

            if (!string.IsNullOrEmpty(user.Email))
            {
                var email = new TextBlock
                {
                    Text = user.Email,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Left,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                SetThemeResource(email, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                textPanel.Children.Add(email);
            }

            Grid.SetColumn(textPanel, 2);

            grid.Children.Add(checkBox);
            grid.Children.Add(avatar);
            grid.Children.Add(textPanel);

            container.Child = grid;

            container.MouseEnter += (s, e) =>
            {
                SetThemeResource(container, Border.BackgroundProperty, "ThemeBorderLightBrush");
            };

            container.MouseLeave += (s, e) =>
            {
                container.Background = Brushes.Transparent;
            };

            container.MouseLeftButtonDown += (s, e) =>
            {
                checkBox.IsChecked = !checkBox.IsChecked;
            };

            return container;
        }

        private async Task HandleVoiceRecordingAsync()
        {
            if (_chatController.CurrentChatId < 0)
            {
                MessageBox.ShowWarning(DefaultLanguage.SelectChat, DefaultLanguage.ErrorTitle);
                return;
            }

            if (!_isRecording)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] Starting recording...");

                    _voiceRecorder = new VoiceRecorder();
                    _voiceRecorder.StartRecording();
                    _isRecording = true;

                    try
                    {
                        var currentText = MessageTextBox.Text ?? string.Empty;
                        _messageTextBeforeRecording = MainPageInputHelper.IsPlaceholderText(currentText) ? string.Empty : currentText;
                        MessageTextBox.IsEnabled = false;
                        ApplyTextInputPlaceholderStyle(MessageTextBox, DefaultLanguage.Loading);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VOICE] Setting recording UI failed: {ex}"); }

                    SendButton.Content = MainPageGlyphs.Stop;
                    SetThemeResource(SendButton, Control.BackgroundProperty, "ThemeDangerBrush");
                    SendButton.Tag = "recording";
                    SendButton.ToolTip = "Stop and send voice message";

                    System.Diagnostics.Debug.WriteLine("[VOICE] Recording started successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VOICE] Failed to start recording: {ex.Message}");
                    MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);

                    _isRecording = false;
                    SendButton.Content = MainPageGlyphs.Microphone;
                    SendButton.Background = Brushes.Transparent;
                    SendButton.Tag = "voice";
                }
            }
            else
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] Stopping recording...");

                    var audioPath = _voiceRecorder?.StopRecording();
                    _isRecording = false;

                    try
                    {
                        MessageTextBox.IsEnabled = true;
                        RestoreMessageInputText(_messageTextBeforeRecording);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VOICE] Restoring message box state failed: {ex}"); }

                    ResetSendButtonForCurrentMessageInput();

                    System.Diagnostics.Debug.WriteLine($"[VOICE] Recording stopped. File path: {audioPath}");

                    if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
                    {
                        var fileInfo = new FileInfo(audioPath);
                        System.Diagnostics.Debug.WriteLine($"[VOICE] File size: {fileInfo.Length} bytes");

                        if (fileInfo.Length < 100)
                        {
                            MessageBox.ShowWarning("Recording is too short or empty", "Error");
                            try { File.Delete(audioPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VOICE] Failed to delete short recording: {ex}"); }
                            return;
                        }

                        await SendVoiceMessageAsync(audioPath);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[VOICE] File not found or path is empty. Path: {audioPath ?? "null"}");
                        MessageBox.ShowWarning("Recording failed or file not found", "Error");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VOICE] Failed to stop recording: {ex.Message}");
                    MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);

                    _isRecording = false;
                    ResetSendButtonForCurrentMessageInput();
                }
                finally
                {
                    _voiceRecorder?.Dispose();
                    _voiceRecorder = null;
                }
            }
        }

        private async Task SendVoiceMessageAsync(string audioPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[VOICE] Starting upload. File: {audioPath}");

                if (!File.Exists(audioPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[VOICE] File does not exist: {audioPath}");
                    MessageBox.ShowError(DefaultLanguage.DownloadFailed, DefaultLanguage.ErrorTitle);
                    return;
                }

                SendButton.IsEnabled = false;
                SendButton.Content = MainPageGlyphs.Loading;

                var uploadResult = await App.ApiClients.Files.UploadFileAsync(audioPath);

                if (!uploadResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[VOICE] Upload failed: {uploadResult.Error}");
                    MessageBox.ShowError(string.Format(DefaultLanguage.UploadFailed, uploadResult.Error), DefaultLanguage.ErrorTitle);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[VOICE] File uploaded successfully: {uploadResult.Url}");

                var message = new CreateMessageDto
                {
                    ChatId = _chatController.CurrentChatId,
                    Text = string.Empty,
                    Type = 1,
                    ContentUrl = uploadResult.Url,
                    FileName = uploadResult.FileName
                };

                System.Diagnostics.Debug.WriteLine($"[VOICE] Sending message. ChatId: {message.ChatId}, Type: {message.Type}, URL: {message.ContentUrl}");

                bool success = await App.HubService.SendMessageAsync(message);

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] Failed to send message via SignalR");
                    MessageBox.ShowError(DefaultLanguage.FailedSendMessage, DefaultLanguage.ErrorTitle);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] Voice message sent successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VOICE] Error sending voice message: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
            finally
            {
                SendButton.IsEnabled = true;
                ResetSendButtonForCurrentMessageInput();

                try
                {
                    if (File.Exists(audioPath))
                    {
                        File.Delete(audioPath);
                        System.Diagnostics.Debug.WriteLine($"[VOICE] Temp file deleted: {audioPath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VOICE] Failed to delete temp file: {ex.Message}");
                }
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double pageWidth = e.NewSize.Width;

            if (pageWidth < 900)
            {
                LeftColumn.Width = new GridLength(250);
                ContactInfoPanel.Width = 350;
            }
            else if (pageWidth < 1200)
            {
                LeftColumn.Width = new GridLength(280);
                ContactInfoPanel.Width = 380;
            }
            else
            {
                LeftColumn.Width = new GridLength(300);
                ContactInfoPanel.Width = 400;
            }

            if (pageWidth < 600)
            {
                CreateGroupDialog.Width = Math.Min(450, pageWidth - 50);
                SearchResultsBorder.Width = Math.Min(300, pageWidth - 100);
            }
            else
            {
                CreateGroupDialog.Width = 500;
                SearchResultsBorder.Width = 340;
            }
        }

        private async void CallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_chatController == null || _chatController.CurrentChatId < 0)
                {
                    MessageBox.ShowWarning(DefaultLanguage.SelectChat, DefaultLanguage.StartCall);
                    return;
                }

                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w is CallWindow cw)
                    {
                        try
                        {
                            if (!cw.IsVisible)
                            {
                                cw.Owner = System.Windows.Application.Current.MainWindow;
                                cw.Show();
                            }

                            if (cw.WindowState == WindowState.Minimized)
                            {
                                cw.WindowState = WindowState.Normal;
                            }

                            cw.Topmost = true;
                            cw.Topmost = false;
                            cw.Activate();

                            try
                            {
                                cw.RegisterHubHandlers();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CALL] RegisterHubHandlers for existing CallWindow failed: {ex}");
                            }

                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CALL] Failed to reopen existing CallWindow: {ex}");
                            break;
                        }
                    }
                }

                var result = MessageBox.ShowQuestion(DefaultLanguage.StartCall, DefaultLanguage.StartCall);
                if (result != MessageBoxResult.Yes) return;

                var callUid = Guid.NewGuid().ToString("N");

                System.Diagnostics.Debug.WriteLine($"[CALL] Starting call for chat {_chatController.CurrentChatId}, callUid={callUid}");
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[CALL] HubService.IsConnected={App.HubService?.IsConnected}");
                }
                catch { }

                try
                {
                    await App.HubService.StartCallAsync(_chatController.CurrentChatId, callUid, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CALL] StartCallAsync threw: {ex}");
                    MessageBox.ShowError($"Failed to start call: {ex.Message}", "Error");
                    return;
                }

                try
                {
                    var win = new CallWindow();
                    win.Owner = System.Windows.Application.Current.MainWindow;
                    try { win.RegisterHubHandlers(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CALL] RegisterHubHandlers failed: {ex}"); }
                    win.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CALL] Failed to open CallWindow: {ex}");
                    MessageBox.ShowError($"Failed to open call window: {ex.Message}", "Error");
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"{DefaultLanguage.CallFailed}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }
    }
}
