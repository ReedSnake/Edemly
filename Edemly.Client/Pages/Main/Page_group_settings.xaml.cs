#nullable disable

using Edemly.Client.Api;
using Edemly.Client.Application.Localization;
using Edemly.Client.Models;
using Edemly.Client.Presentation.Common;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Pages.Main
{
    public partial class Page_group_settings : ThemedPage
    {
        private readonly Contact _groupContact;
        private readonly int _chatId;
        private readonly IApiService _apiService;
        private List<ChatMemberDto> _groupMembers = new();
        private bool _isOwner;

        private string _originalGroupName = string.Empty;
        private string _originalGroupDescription = string.Empty;
        private string _originalIconUrl = string.Empty;

        private string _pendingIconPath = null;
        private bool _iconChanged;

        public Page_group_settings(Contact groupContact, int chatId)
        {
            InitializeComponent();
            _groupContact = groupContact;
            _chatId = chatId;
            _apiService = App.ApiService;

            LoadGroupData();
            _ = LoadGroupMembersAsync();

            _ = CheckOwnerStatusAndEnableChangeIconAsync();

            GroupNameTextBox.TextChanged += GroupNameTextBox_TextChanged;
            GroupDescriptionTextBox.TextChanged += GroupDescriptionTextBox_TextChanged;
        }

        protected override void ApplyTheme()
        {
            if (Content is Grid rootGrid)
                rootGrid.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");

            ApplyTextBoxStateResources(GroupNameTextBox);
            ApplyTextBoxStateResources(GroupDescriptionTextBox);

            System.Diagnostics.Debug.WriteLine("[PAGE_GROUP_SETTINGS] Theme applied");
        }

        private void ApplyTextBoxStateResources(TextBox textBox)
        {
            if (textBox == null)
                return;

            textBox.SetResourceReference(
                Control.BackgroundProperty,
                textBox.IsReadOnly ? "ThemeSurfaceAltBrush" : "ThemeInputBackgroundBrush");
        }

        private TextBlock CreateCenteredStatusText(string text, string foregroundResourceKey = "ThemeTextSecondaryBrush")
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            textBlock.SetResourceReference(TextBlock.ForegroundProperty, foregroundResourceKey);

            return textBlock;
        }

        private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonVisibility();
        }

        private void GroupDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonVisibility();
        }

        private void UpdateSaveButtonVisibility()
        {
            try
            {
                if (!_isOwner)
                {
                    SaveButton.Visibility = Visibility.Collapsed;
                    HeaderSaveButton.Visibility = Visibility.Collapsed;
                    return;
                }

                var currentName = GroupNameTextBox.Text?.Trim() ?? string.Empty;
                var currentDescription = GroupDescriptionTextBox.Text?.Trim() ?? string.Empty;

                bool hasTextChanges = (currentName != _originalGroupName) ||
                                     (currentDescription != _originalGroupDescription);

                bool hasChanges = hasTextChanges || _iconChanged;

                SaveButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
                HeaderSaveButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] HasChanges: {hasChanges}, TextChanges: {hasTextChanges}, IconChanged: {_iconChanged}, IsOwner: {_isOwner}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error updating save button visibility: {ex.Message}");
            }
        }

        private async Task CheckOwnerStatusAndEnableChangeIconAsync()
        {
            try
            {
                var members = await _apiService.GetChatMembersAsync(_chatId);

                _isOwner = members?.Any(m => m.UserId == App.CurrentUserId && m.Role == 1) ?? false;

                if (_isOwner)
                {
                    ChangeIconButton.Visibility = Visibility.Visible;
                    ChangeIconButton.IsEnabled = true;
                    ChangeIconButton.Opacity = 1.0;
                    ChangeIconButton.ToolTip = DefaultLanguage.ChangeIcon;

                    GroupNameTextBox.IsReadOnly = false;
                    ApplyTextBoxStateResources(GroupNameTextBox);

                    GroupDescriptionTextBox.IsReadOnly = false;
                    ApplyTextBoxStateResources(GroupDescriptionTextBox);
                }
                else
                {
                    ChangeIconButton.Visibility = Visibility.Collapsed;

                    GroupNameTextBox.IsReadOnly = true;
                    ApplyTextBoxStateResources(GroupNameTextBox);

                    GroupDescriptionTextBox.IsReadOnly = true;
                    ApplyTextBoxStateResources(GroupDescriptionTextBox);

                    SaveButton.Visibility = Visibility.Collapsed;
                    HeaderSaveButton.Visibility = Visibility.Collapsed;
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Is owner: {_isOwner}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error checking owner status: {ex.Message}");
                _isOwner = false;
                ChangeIconButton.Visibility = Visibility.Collapsed;
                GroupNameTextBox.IsReadOnly = true;
                GroupDescriptionTextBox.IsReadOnly = true;
                ApplyTextBoxStateResources(GroupNameTextBox);
                ApplyTextBoxStateResources(GroupDescriptionTextBox);
                SaveButton.Visibility = Visibility.Collapsed;
                HeaderSaveButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadGroupData()
        {
            GroupNameTextBox.Text = _groupContact.Name;
            _originalGroupName = _groupContact.Name?.Trim() ?? string.Empty;

            GroupDescriptionTextBox.Text = string.Empty;
            _originalGroupDescription = string.Empty;

            _originalIconUrl = _groupContact.PhotoPath ?? string.Empty;

            _iconChanged = false;
            _pendingIconPath = null;

            SaveButton.Visibility = Visibility.Collapsed;
            HeaderSaveButton.Visibility = Visibility.Collapsed;

            _ = LoadGroupIconAsync();
            _ = LoadGroupDetailsAsync();
        }

        private async Task LoadGroupDetailsAsync()
        {
            try
            {
                var chatData = await _apiService.GetChatByIdAsync(_chatId);

                if (chatData != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrEmpty(chatData.Description))
                        {
                            GroupDescriptionTextBox.Text = chatData.Description;
                            _originalGroupDescription = chatData.Description.Trim();
                        }

                        System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Loaded chat details: name={chatData.Name}, description={chatData.Description}");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading group details: {ex.Message}");
            }
        }

        private async Task LoadGroupIconAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_groupContact.PhotoPath) &&
                    _groupContact.PhotoPath != "pack://application:,,,/Assets/Avatars/default-avatar.png")
                {
                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(_groupContact.PhotoPath);
                    if (bitmap != null)
                    {
                        GroupIconImage.ImageSource = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading icon: {ex.Message}");
            }
        }

        private void ShowIconPreview(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                GroupIconImage.ImageSource = bitmap;
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Showing preview for: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error showing preview: {ex.Message}");
            }
        }

        private async Task LoadGroupMembersAsync()
        {
            try
            {
                GroupMembersPanel.Children.Clear();

                var loadingText = CreateCenteredStatusText(DefaultLanguage.LoadingMembers);
                GroupMembersPanel.Children.Add(loadingText);

                _groupMembers = await _apiService.GetChatMembersAsync(_chatId);

                if (_groupMembers == null || _groupMembers.Count == 0)
                {
                    GroupMembersPanel.Children.Clear();
                    var noMembersText = CreateCenteredStatusText(DefaultLanguage.NoMembers);
                    GroupMembersPanel.Children.Add(noMembersText);
                    return;
                }

                GroupMembersPanel.Children.Clear();

                foreach (var member in _groupMembers)
                {
                    var memberItem = CreateMemberItem(member);
                    GroupMembersPanel.Children.Add(memberItem);
                    _ = LoadMemberDetailsAsync(member, memberItem);
                }

                var statsText = new TextBlock
                {
                    Text = string.Format(DefaultLanguage.TotalMembers, _groupMembers.Count),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                statsText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                GroupMembersPanel.Children.Add(statsText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading members: {ex.Message}");

                GroupMembersPanel.Children.Clear();
                var errorText = CreateCenteredStatusText(DefaultLanguage.FailedLoadMembers, "ThemeDangerBrush");
                GroupMembersPanel.Children.Add(errorText);
            }
        }

        private Border CreateMemberItem(ChatMemberDto member)
        {
            var container = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 5),
                Tag = member
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            avatarBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBorderLightBrush");

            var placeholderText = new TextBlock
            {
                Text = member.UserId.ToString().Length > 2
                    ? member.UserId.ToString().Substring(member.UserId.ToString().Length - 2)
                    : member.UserId.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            placeholderText.SetResourceReference(TextBlock.ForegroundProperty, "ThemePrimaryBrush");
            avatarBorder.Child = placeholderText;

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var userIdText = new TextBlock
            {
                Text = string.Format(DefaultLanguage.UserIdText, member.UserId),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            userIdText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
            textPanel.Children.Add(userIdText);

            var loadingText = new TextBlock
            {
                Text = DefaultLanguage.LoadingText,
                FontSize = 11,
                FontStyle = FontStyles.Italic
            };
            loadingText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
            textPanel.Children.Add(loadingText);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var roleBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            roleBadge.SetResourceReference(
                Border.BackgroundProperty,
                member.Role == 1 ? "ThemePrimaryBrush" : "ThemeTextSecondaryBrush");

            var roleText = new TextBlock
            {
                Text = member.Role == 1 ? DefaultLanguage.OwnerRole : DefaultLanguage.MemberRole,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            roleText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeOnPrimaryTextBrush");
            roleBadge.Child = roleText;

            Grid.SetColumn(roleBadge, 2);
            grid.Children.Add(roleBadge);

            container.Child = grid;

            container.MouseEnter += (s, e) =>
            {
                container.SetResourceReference(Border.BackgroundProperty, "ThemeBorderLightBrush");
            };

            container.MouseLeave += (s, e) =>
            {
                container.Background = Brushes.Transparent;
            };

            container.MouseLeftButtonDown += async (s, e) =>
            {
                await ShowMemberOptionsAsync(member);
            };

            return container;
        }

        private async Task LoadMemberDetailsAsync(ChatMemberDto member, Border memberItem)
        {
            try
            {
                var user = await _apiService.GetUserByIdAsync(member.UserId);

                if (user != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var grid = memberItem.Child as Grid;
                        if (grid != null)
                        {
                            var textPanel = grid.Children
                                .OfType<StackPanel>()
                                .FirstOrDefault(p => Grid.GetColumn(p) == 1);

                            if (textPanel != null && textPanel.Children.Count >= 2)
                            {
                                var nameText = textPanel.Children[0] as TextBlock;
                                if (nameText != null)
                                {
                                    nameText.Text = user.Username ?? string.Format(DefaultLanguage.UserIdText, member.UserId);
                                }

                                var statusText = textPanel.Children[1] as TextBlock;
                                if (statusText != null)
                                {
                                    statusText.Text = user.Email ?? DefaultLanguage.MemberRole;
                                    statusText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                                    statusText.FontStyle = FontStyles.Normal;
                                }
                            }

                            var avatarBorder = grid.Children
                                .OfType<Border>()
                                .FirstOrDefault(b => Grid.GetColumn(b) == 0);

                            if (avatarBorder != null && !string.IsNullOrEmpty(user.PfpUrl))
                            {
                                _ = LoadAvatarAsync(avatarBorder, user.PfpUrl);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading member details: {ex.Message}");
            }
        }

        private async Task LoadAvatarAsync(Border avatarBorder, string pfpUrl)
        {
            try
            {
                var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(pfpUrl);
                if (bitmap != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        avatarBorder.Background = new ImageBrush
                        {
                            ImageSource = bitmap,
                            Stretch = Stretch.UniformToFill
                        };
                        avatarBorder.Child = null;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading avatar: {ex.Message}");
            }
        }

        private async Task ShowMemberOptionsAsync(ChatMemberDto member)
        {
        }

        private async void ChangeIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOwner)
            {
                MessageBox.Show(DefaultLanguage.OwnerOnlyChangeIcon, DefaultLanguage.PermissionDenied,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                FilterIndex = 1,
                Title = DefaultLanguage.SelectGroupIcon
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _pendingIconPath = openFileDialog.FileName;
                    _iconChanged = true;

                    ShowIconPreview(_pendingIconPath);

                    UpdateSaveButtonVisibility();

                    System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Icon selected for preview: {_pendingIconPath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(DefaultLanguage.ErrorText, ex.Message), DefaultLanguage.ErrorTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    _pendingIconPath = null;
                    _iconChanged = false;
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isOwner)
                {
                    MessageBox.Show(DefaultLanguage.OwnerOnlyChangeSettings, DefaultLanguage.PermissionDenied,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newName = GroupNameTextBox.Text?.Trim();
                string newDescription = GroupDescriptionTextBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show(DefaultLanguage.GroupNameEmpty, "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveButton.IsEnabled = false;
                HeaderSaveButton.IsEnabled = false;

                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Saving changes for chat {_chatId}");

                string finalIconUrl = _originalIconUrl;

                if (_iconChanged && !string.IsNullOrEmpty(_pendingIconPath) && File.Exists(_pendingIconPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Uploading new icon: {_pendingIconPath}");

                    ChangeIconButton.IsEnabled = false;

                    var uploadResult = await _apiService.UploadGroupIconAsync(_chatId, _pendingIconPath);

                    if (uploadResult.Success && !string.IsNullOrEmpty(uploadResult.Url))
                    {
                        finalIconUrl = uploadResult.Url;
                        System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Icon uploaded successfully: {finalIconUrl}");

                        if (!string.IsNullOrEmpty(_originalIconUrl))
                        {
                            try { App.GlobalProfilePictureCache.InvalidateCache(_originalIconUrl); } catch { }
                        }

                        _groupContact.PhotoPath = finalIconUrl;

                        try
                        {
                            await App.GlobalProfilePictureCache.ForceDownloadAsync(finalIconUrl);
                        }
                        catch { }

                        if (App.GlobalChatController != null)
                        {
                            App.GlobalChatController.UpdateGroupIcon(_chatId, finalIconUrl);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Icon upload failed: {uploadResult.Error}");
                        MessageBox.Show(string.Format(DefaultLanguage.IconUploadFailed, uploadResult.Error),
                            DefaultLanguage.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    ChangeIconButton.IsEnabled = true;
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Updating chat {_chatId}: name='{newName}', description='{newDescription}'");

                var result = await _apiService.UpdateChatAsync(_chatId, name: newName, description: newDescription);

                if (result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Server update successful");

                    _originalGroupName = newName ?? string.Empty;
                    _originalGroupDescription = newDescription ?? string.Empty;
                    _originalIconUrl = finalIconUrl;

                    _iconChanged = false;
                    _pendingIconPath = null;

                    _groupContact.Name = newName;

                    if (App.GlobalChatController != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            App.GlobalChatController.UpdateChatButtonName(_chatId, newName);
                        });
                    }

                    if (App.HubService != null)
                    {
                        await App.HubService.NotifyGroupUpdateAsync(_chatId, newName, newDescription, finalIconUrl);
                        System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Notified all members via Hub");
                    }

                    SaveButton.Visibility = Visibility.Collapsed;
                    HeaderSaveButton.Visibility = Visibility.Collapsed;

                    MessageBox.Show(DefaultLanguage.GroupSettingsUpdated, DefaultLanguage.SuccessTitle,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Server update failed: {result.Error}");
                    MessageBox.Show(string.Format(DefaultLanguage.FailedUpdate, result.Error),
                        DefaultLanguage.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Exception: {ex.Message}");
                MessageBox.Show(string.Format(DefaultLanguage.ErrorText, ex.Message),
                    DefaultLanguage.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                HeaderSaveButton.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
        }

        public async Task RefreshMembersListAsync()
        {
            await LoadGroupMembersAsync();
        }
    }
}
