#nullable disable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Edemly.Client.Models;
using Edemly.Client.Services.Api;
using Edemly.Client.DTOs;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Edemly.Client.Lang;
using Edemly.Client.Services;

namespace Edemly.Client.Pages
{
    public partial class Page_group_settings : Page
    {
        private readonly Contact _groupContact;
        private readonly int _chatId;
        private readonly IApiService _apiService;
        private List<string> _groupNotes = new List<string>();
        private List<ChatMemberDto> _groupMembers = new List<ChatMemberDto>();
        private bool _isOwner = false;

        private string _originalGroupName = string.Empty;
        private string _originalGroupDescription = string.Empty;
        private string _originalIconUrl = string.Empty;

        private string _pendingIconPath = null; 
        private string _newIconUrl = null; 
        private bool _iconChanged = false;

        public Page_group_settings(Contact groupContact, int chatId)
        {
            InitializeComponent();
            _groupContact = groupContact;
            _chatId = chatId;
            _apiService = App.ApiService;

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            LoadGroupData();
            LoadGroupMembers();

            _ = CheckOwnerStatusAndEnableChangeIconAsync();

            GroupNameTextBox.TextChanged += GroupNameTextBox_TextChanged;
            GroupDescriptionTextBox.TextChanged += GroupDescriptionTextBox_TextChanged;
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine("[PAGE_GROUP_SETTINGS] Theme changed");
            }
            catch { }
        }

        private void ApplyThemeToPage()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                var grid = this.Content as Grid;
                if (grid != null)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(1, 1),
                        EndPoint = new Point(0, 0)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop(palette.BackgroundDark, 0.7));
                    gradientBrush.GradientStops.Add(new GradientStop(palette.Primary, 0.0));
                    grid.Background = gradientBrush;
                }

                if (SaveButton != null)
                {
                    SaveButton.Background = new SolidColorBrush(palette.Primary);
                }
                if (HeaderSaveButton != null)
                {
                    HeaderSaveButton.Background = new SolidColorBrush(palette.Primary);
                }
                if (ChangeIconButton != null)
                {
                    ChangeIconButton.Background = new SolidColorBrush(palette.Primary);
                }

                var groupIconBorder = this.FindName("GroupIconBorder") as Border;
                if (groupIconBorder != null)
                {
                    groupIconBorder.BorderBrush = new SolidColorBrush(palette.Secondary);
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_GROUP_SETTINGS] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_GROUP_SETTINGS] ApplyThemeToPage error: {ex.Message}");
            }
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
                    GroupNameTextBox.Background = new SolidColorBrush(Colors.White);

                    GroupDescriptionTextBox.IsReadOnly = false;
                    GroupDescriptionTextBox.Background = new SolidColorBrush(Colors.White);
                }
                else
                {
                    ChangeIconButton.Visibility = Visibility.Collapsed;

                    GroupNameTextBox.IsReadOnly = true;
                    GroupNameTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));

                    GroupDescriptionTextBox.IsReadOnly = true;
                    GroupDescriptionTextBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));

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
            _newIconUrl = null;

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
                    await Application.Current.Dispatcher.InvokeAsync(() =>
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

        /// <summary>
        /// Завантажує та показує превью локального файлу іконки
        /// </summary>
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

        private async Task LoadGroupMembers()
        {
            try
            {
                GroupMembersPanel.Children.Clear();

                var loadingText = new TextBlock
                {
                    Text = DefaultLanguage.LoadingMembers,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                GroupMembersPanel.Children.Add(loadingText);

                _groupMembers = await _apiService.GetChatMembersAsync(_chatId);

                if (_groupMembers == null || _groupMembers.Count == 0)
                {
                    GroupMembersPanel.Children.Clear();
                    var noMembersText = new TextBlock
                    {
                        Text = DefaultLanguage.NoMembers,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                GroupMembersPanel.Children.Add(statsText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading members: {ex.Message}");

                GroupMembersPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = DefaultLanguage.FailedLoadMembers,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Red),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10)
                };
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
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F0F0"))
            };

            var placeholderText = new TextBlock
            {
                Text = member.UserId.ToString().Length > 2
                    ? member.UserId.ToString().Substring(member.UserId.ToString().Length - 2)
                    : member.UserId.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#057272")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = placeholderText;

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var userIdText = new TextBlock
            {
                Text = string.Format(DefaultLanguage.UserIdText, member.UserId),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#031C1C"))
            };
            textPanel.Children.Add(userIdText);

            var loadingText = new TextBlock
            {
                Text = DefaultLanguage.LoadingText,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray),
                FontStyle = FontStyles.Italic
            };
            textPanel.Children.Add(loadingText);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var roleBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = member.Role == 1 ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#057272")) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575"))
            };

            var roleText = new TextBlock
            {
                Text = member.Role == 1 ? DefaultLanguage.OwnerRole : DefaultLanguage.MemberRole,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            roleBadge.Child = roleText;

            Grid.SetColumn(roleBadge, 2);
            grid.Children.Add(roleBadge);

            container.Child = grid;

            container.MouseEnter += (s, e) =>
            {
                container.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F8F8"));
            };

            container.MouseLeave += (s, e) =>
            {
                container.Background = Brushes.Transparent;
            };

            container.MouseLeftButtonDown += async (s, e) =>
            {
                await ShowMemberOptions(member);
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
                    await Application.Current.Dispatcher.InvokeAsync(() =>
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
                                    statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666"));
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
                    await Application.Current.Dispatcher.InvokeAsync(() =>
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

        private async Task ShowMemberOptions(ChatMemberDto member)
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
                        _newIconUrl = uploadResult.Url;

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

                        if (App.GlobalChatManager != null)
                        {
                            App.GlobalChatManager.UpdateGroupIcon(_chatId, finalIconUrl);
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

                    if (App.GlobalChatManager != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            App.GlobalChatManager.UpdateChatButtonName(_chatId, newName);

                            if (App.GlobalChatManager.CurrentChatId == _chatId &&
                                App.GlobalChatManager.CurrentChatContact != null)
                            {
                                App.GlobalChatManager.CurrentChatContact.Name = newName;
                            }
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

        public async Task RefreshMembersList()
        {
            await LoadGroupMembers();
        }
    }
}