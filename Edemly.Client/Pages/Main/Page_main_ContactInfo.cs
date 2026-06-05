#nullable disable

using Edemly.Client.Application.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
namespace Edemly.Client.Pages.Main
{
    public partial class Page_main
    {
        private void ContactMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatController?.IsCurrentChatGroup() == true)
            {
                if (isGroupInfoOpen)
                {
                    CloseGroupInfo();
                }
                else
                {
                    OpenGroupInfo();
                }
            }
            else
            {
                if (isContactInfoOpen)
                {
                    CloseContactInfo();
                }
                else
                {
                    OpenContactInfo();
                }
            }
        }

        private async void OpenContactInfo()
        {
            if (_chatController?.CurrentChatContact == null) return;

            isContactInfoOpen = true;
            ContactInfoOverlay.Visibility = Visibility.Visible;
            ContactInfoPanel.Visibility = Visibility.Visible;

            var contact = _chatController.CurrentChatContact;
            await RefreshCurrentContactDetailsAsync(contact);
            PopulateContactInfo(contact);
            await LoadContactPhotoAsync(contact);

            await LoadContactNotesAsync();

            EditContactButton.Visibility = Visibility.Visible;

            DoubleAnimation animation = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ContactInfoTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private async Task RefreshCurrentContactDetailsAsync(Models.Contact contact)
        {
            try
            {
                if (_chatController?.IsCurrentChatGroup() == true)
                {
                    return;
                }

                var user = await App.ApiService.GetUserByIdAsync(contact.UserId);
                if (user == null)
                {
                    return;
                }

                contact.Username = user.Username ?? contact.Username;
                contact.FirstName = user.FirstName ?? contact.FirstName;
                contact.LastName = user.LastName ?? contact.LastName;
                contact.Email = user.Email ?? contact.Email;
                contact.Phone = user.PhoneNumber ?? contact.Phone;
                contact.PhotoPath = string.IsNullOrWhiteSpace(user.PfpUrl) ? contact.PhotoPath : user.PfpUrl;
                contact.Name = Models.Contact.ResolveDisplayName(contact.Name, contact.Username, contact.FirstName, contact.LastName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT INFO] Failed to refresh contact details: {ex.Message}");
            }
        }

        private void PopulateContactInfo(Models.Contact contact)
        {
            ContactInfoName.Text = contact.DisplayName ?? string.Empty;
            ContactInfoUsername.Text = GetContactValueOrFallback(contact.Username, DefaultLanguage.ContactNameUnknown);
            ContactInfoFirstName.Text = GetContactValueOrFallback(contact.FirstName, DefaultLanguage.ContactNameUnknown);
            ContactInfoLastName.Text = GetContactValueOrFallback(contact.LastName, DefaultLanguage.ContactNameUnknown);
            ContactInfoEmail.Text = GetContactValueOrFallback(contact.Email, DefaultLanguage.ContactEmailNotSpecified);
            ContactInfoPhone.Text = GetContactValueOrFallback(contact.Phone, DefaultLanguage.ContactPhoneNotSpecified);
        }

        private async Task LoadContactPhotoAsync(Models.Contact contact)
        {
            try
            {
                if (!string.IsNullOrEmpty(contact.PhotoPath) &&
                    contact.PhotoPath != "pack://application:,,,/Assets/Avatars/default-avatar.png")
                {
                    System.Diagnostics.Debug.WriteLine($"[CONTACT INFO] Loading photo from: {contact.PhotoPath}");

                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(contact.PhotoPath);

                    if (bitmap != null)
                    {
                        ContactPhotoBackground.ImageSource = bitmap;
                        System.Diagnostics.Debug.WriteLine("[CONTACT INFO] Photo loaded successfully");
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("[CONTACT INFO] Failed to load photo, using default");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CONTACT INFO] Using default avatar");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT INFO] Error loading photo: {ex.Message}");
            }

            ContactPhotoBackground.ImageSource = new BitmapImage(
                new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
        }

        private static string GetContactValueOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void CloseContactInfo()
        {
            isContactInfoOpen = false;

            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = 400,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ContactInfoTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            Task.Delay(300).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ContactInfoPanel.Visibility = Visibility.Collapsed;
                    ContactInfoOverlay.Visibility = Visibility.Collapsed;
                }));
            });
        }

        private void ContactInfoOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CloseContactInfo();
        }

        private void CloseContactInfo_Click(object sender, RoutedEventArgs e)
        {
            CloseContactInfo();
        }

        private void ContactInfoText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                TextBlock textBlock = sender as TextBlock;
                if (textBlock != null && !string.IsNullOrEmpty(textBlock.Text))
                {
                    Clipboard.SetText(textBlock.Text);
                }
            }
        }

        private void EditContactButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatController?.CurrentChatContact != null)
            {
                ContactNoteEditor.Focus();
                ContactNoteEditor.SelectAll();
            }
        }

        private async void OpenGroupInfo()
        {
            if (_chatController?.CurrentChatContact == null || _chatController.CurrentChatId < 0) return;

            isGroupInfoOpen = true;
            GroupInfoOverlay.Visibility = Visibility.Visible;
            GroupInfoPanel.Visibility = Visibility.Visible;

            var groupContact = _chatController.CurrentChatContact;
            GroupInfoNameText.Text = groupContact.Name ?? string.Empty;

            await LoadGroupPhotoAsync();

            await LoadGroupDescriptionAsync();

            await LoadGroupMembersAsync();

            await ShowGroupSettingsIconIfOwnerAsync();

            var animation = new DoubleAnimation
            {
                From = 400,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            GroupInfoTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private async Task ShowGroupSettingsIconIfOwnerAsync()
        {
            try
            {
                var settingsButton = this.FindName("GroupSettingsIconButton") as Button;
                if (settingsButton == null) return;

                var members = await App.ApiService.GetChatMembersAsync(_chatController.CurrentChatId);

                var isOwner = members?.Any(m => m.UserId == App.CurrentUserId && m.Role == 1) ?? false;

                settingsButton.Visibility = isOwner ? Visibility.Visible : Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Is owner: {isOwner}, Settings icon visibility: {settingsButton.Visibility}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error checking owner status: {ex.Message}");
                var settingsButton = this.FindName("GroupSettingsIconButton") as Button;
                if (settingsButton != null) settingsButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadGroupDescriptionAsync()
        {
            try
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading description: {ex.Message}");
            }
        }

        private async Task LoadGroupPhotoAsync()
        {
            try
            {
                if (_chatController?.CurrentChatContact == null) return;

                var groupContact = _chatController.CurrentChatContact;

                if (!string.IsNullOrEmpty(groupContact.PhotoPath) &&
                    groupContact.PhotoPath != "pack://application:,,,/Assets/Avatars/default-avatar.png")
                {
                    System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Loading group photo from: {groupContact.PhotoPath}");

                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(groupContact.PhotoPath);
                    if (bitmap != null)
                    {
                        GroupPhotoBackground.ImageSource = bitmap;
                        System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Group photo loaded successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Failed to load photo, using default");
                        GroupPhotoBackground.ImageSource = new BitmapImage(
                            new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GROUP INFO] No photo path, using default avatar");
                    GroupPhotoBackground.ImageSource = new BitmapImage(
                        new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading group photo: {ex.Message}");
                GroupPhotoBackground.ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
            }
        }

        private async Task LoadGroupMembersAsync()
        {
            GroupMembersPanel.Children.Clear();

            try
            {
                var members = await App.ApiService.GetChatMembersAsync(_chatController.CurrentChatId);

                if (members == null || members.Count == 0)
                {
                    var noMembersText = new TextBlock
                    {
                        Text = DefaultLanguage.NoMembers,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    GroupMembersPanel.Children.Add(noMembersText);
                    return;
                }

                foreach (var member in members)
                {
                    var memberItem = CreateGroupMemberItem(member, null);
                    GroupMembersPanel.Children.Add(memberItem);

                    _ = LoadMemberDataAsync(member, memberItem);
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Loaded {GroupMembersPanel.Children.Count} member items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading members: {ex.Message}");
            }
        }

        private async Task LoadMemberDataAsync(ChatMemberDto member, Border memberItem)
        {
            try
            {
                var user = await App.ApiService.GetUserByIdAsync(member.UserId);

                if (user != null)
                {
                    var newItem = CreateGroupMemberItem(member, user);

                    int index = GroupMembersPanel.Children.IndexOf(memberItem);
                    if (index >= 0)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            GroupMembersPanel.Children.RemoveAt(index);
                            GroupMembersPanel.Children.Insert(index, newItem);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading member {member.UserId}: {ex.Message}");
            }
        }

        private Border CreateGroupMemberItem(ChatMemberDto member, UserDto user)
        {
            var container = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var grid = new Grid { Margin = new Thickness(0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (user != null && !string.IsNullOrEmpty(user.PfpUrl))
            {
                var imageBrush = new ImageBrush { Stretch = Stretch.UniformToFill };
                avatarBorder.Background = imageBrush;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(user.PfpUrl);
                        if (bitmap != null)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                imageBrush.ImageSource = bitmap;
                            });
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CONTACT_INFO] Failed to load user avatar: {ex}"); }
                });
            }
            else
            {
                avatarBorder.Background = new SolidColorBrush(Color.FromRgb(130, 200, 195));
            }

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var usernameBlock = new TextBlock
            {
                Text = user?.Username ?? string.Format(DefaultLanguage.UserIdText, member.UserId),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#031C1C")),
                Margin = new Thickness(0, 0, 0, 2)
            };
            textPanel.Children.Add(usernameBlock);

            var roleDisplayText = GetSimpleRoleDisplay(member.Role.ToString());
            var roleBlock = new TextBlock
            {
                Text = roleDisplayText,
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                Opacity = 0.8
            };
            textPanel.Children.Add(roleBlock);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            container.Child = grid;

            container.MouseLeftButtonDown += async (s, e) => await ShowMemberOptionsAsync(member, user);

            container.MouseEnter += (s, e) =>
            {
                container.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6FFFD"));
            };
            container.MouseLeave += (s, e) =>
            {
                container.Background = Brushes.Transparent;
            };

            return container;
        }

        private string GetSimpleRoleDisplay(string role)
        {
            if (int.TryParse(role, out var roleInt))
            {
                return roleInt == 1 ? DefaultLanguage.OwnerBadge : DefaultLanguage.MemberRole;
            }

            return role.ToLower() switch
            {
                "admin" => DefaultLanguage.OwnerBadge,
                "creator" => DefaultLanguage.OwnerBadge,
                "base" => DefaultLanguage.MemberRole,
                _ => DefaultLanguage.MemberRole
            };
        }

        private async Task ShowMemberOptionsAsync(ChatMemberDto member, UserDto user)
        {
            try
            {
                var userName = user?.Username ?? string.Format(DefaultLanguage.UserIdText, member.UserId);

                var result = MessageBox.ShowQuestion($"Choose action for {userName}:", DefaultLanguage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    await OpenChatWithUserAsync(member.UserId, user);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error showing member options: {ex.Message}");
            }
        }

        private async Task OpenChatWithUserAsync(int userId, UserDto user)
        {
            try
            {
                if (userId == App.CurrentUserId)
                {
                    MessageBox.ShowWarning(DefaultLanguage.Warning, DefaultLanguage.Information);
                    return;
                }

                CloseGroupInfo();

                var contact = user != null
                    ? Models.Contact.FromUserDto(user)
                    : new Models.Contact(
                        userId,
                        string.Format(DefaultLanguage.UserIdText, userId),
                        string.Empty,
                        string.Empty,
                        "pack://application:,,,/Assets/Avatars/default-avatar.png");

                var chatResult = await App.ApiService.CreateOrGetPrivateChatAsync(userId);
                if (chatResult == null)
                {
                    MessageBox.ShowError(DefaultLanguage.Error, DefaultLanguage.ErrorTitle);
                    return;
                }

                await _chatController.SwitchToChatPublicAsync(contact, chatResult.Id);
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Opened private chat with {user?.Username}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error opening chat: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private void CloseGroupInfo()
        {
            isGroupInfoOpen = false;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 400,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            GroupInfoTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            Task.Delay(300).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GroupInfoPanel.Visibility = Visibility.Collapsed;
                    GroupInfoOverlay.Visibility = Visibility.Collapsed;
                }));
            });
        }

        private void GroupInfoOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseGroupInfo();
        }

        private void CloseGroupInfo_Click(object sender, RoutedEventArgs e)
        {
            CloseGroupInfo();
        }

        private void GroupSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatController.CurrentChatContact != null)
            {
                CloseGroupInfo();
                NavigationService.Navigate(new Page_group_settings(_chatController.CurrentChatContact, _chatController.CurrentChatId));
            }
        }
    }
}
