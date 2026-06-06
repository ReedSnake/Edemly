#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private async void OpenGroupInfo()
        {
            if (_chatController?.CurrentChatContact == null || _chatController.CurrentChatId < 0)
            {
                return;
            }

            isGroupInfoOpen = true;
            PageMainInfoPanelHelper.PrepareToShow(GroupInfoPanel, GroupInfoOverlay);

            var groupContact = _chatController.CurrentChatContact;
            GroupInfoNameText.Text = groupContact.Name ?? string.Empty;

            await Task.WhenAll(
                LoadGroupPhotoAsync(),
                LoadGroupDescriptionAsync());

            var members = await LoadGroupMembersAsync();
            ApplyGroupSettingsVisibility(members);

            PageMainInfoPanelHelper.SlideIn(GroupInfoTransform);
        }

        private void ApplyGroupSettingsVisibility(IReadOnlyCollection<ChatMemberDto> members)
        {
            var isOwner = App.CurrentUserId is int currentUserId
                && members.Any(member => member.UserId == currentUserId && member.Role == 1);

            GroupSettingsIconButton.Visibility = isOwner ? Visibility.Visible : Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Is owner: {isOwner}, settings visibility: {GroupSettingsIconButton.Visibility}");
        }

        private async Task LoadGroupDescriptionAsync()
        {
            try
            {
                var chat = await App.ApiService.GetChatByIdAsync(_chatController.CurrentChatId);
                var description = chat?.Description?.Trim() ?? string.Empty;

                GroupInfoDescriptionText.Text = description;
                GroupInfoDescriptionText.Visibility = string.IsNullOrWhiteSpace(description)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                NoDescriptionText.Visibility = string.IsNullOrWhiteSpace(description)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading description: {ex.Message}");
                GroupInfoDescriptionText.Text = string.Empty;
                GroupInfoDescriptionText.Visibility = Visibility.Collapsed;
                NoDescriptionText.Visibility = Visibility.Visible;
            }
        }

        private Task LoadGroupPhotoAsync()
        {
            var photoPath = _chatController?.CurrentChatContact?.PhotoPath;
            return PageMainAvatarHelper.SetImageSourceAsync(GroupPhotoBackground, photoPath, "[GROUP INFO]");
        }

        private async Task<List<ChatMemberDto>> LoadGroupMembersAsync()
        {
            GroupMembersPanel.Children.Clear();

            try
            {
                var members = await App.ApiService.GetChatMembersAsync(_chatController.CurrentChatId) ?? new List<ChatMemberDto>();

                if (members.Count == 0)
                {
                    ShowGroupMembersPlaceholder(DefaultLanguage.NoMembers);
                    return members;
                }

                var users = await Task.WhenAll(members.Select(member => LoadGroupMemberUserAsync(member.UserId)));

                for (var index = 0; index < members.Count; index++)
                {
                    GroupMembersPanel.Children.Add(CreateGroupMemberItem(members[index], users[index]));
                }

                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Loaded {GroupMembersPanel.Children.Count} member items");
                return members;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading members: {ex.Message}");
                ShowGroupMembersPlaceholder(DefaultLanguage.NoMembers);
                return new List<ChatMemberDto>();
            }
        }

        private async Task<UserDto> LoadGroupMemberUserAsync(int userId)
        {
            try
            {
                return await App.ApiService.GetUserByIdAsync(userId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error loading member {userId}: {ex.Message}");
                return null;
            }
        }

        private void ShowGroupMembersPlaceholder(string text)
        {
            GroupMembersPanel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            });
        }

        private Border CreateGroupMemberItem(ChatMemberDto member, UserDto user)
        {
            var container = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
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

            var avatarBrush = PageMainAvatarHelper.CreateAvatarBrush();
            avatarBorder.Background = avatarBrush;
            _ = PageMainAvatarHelper.SetImageSourceAsync(avatarBrush, user?.PfpUrl, $"[GROUP INFO] Member {member.UserId}");

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var usernameBlock = new TextBlock
            {
                Text = ResolveMemberDisplayName(member, user),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#031C1C")),
                Margin = new Thickness(0, 0, 0, 2)
            };
            textPanel.Children.Add(usernameBlock);

            var roleBlock = new TextBlock
            {
                Text = GetSimpleRoleDisplay(member.Role),
                FontSize = 10,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999")),
                Opacity = 0.8
            };
            textPanel.Children.Add(roleBlock);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            container.Child = grid;
            container.MouseLeftButtonDown += async (_, _) => await ShowMemberOptionsAsync(member, user);
            container.MouseEnter += (_, _) =>
            {
                container.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6FFFD"));
            };
            container.MouseLeave += (_, _) =>
            {
                container.Background = Brushes.Transparent;
            };

            return container;
        }

        private static string ResolveMemberDisplayName(ChatMemberDto member, UserDto user)
        {
            if (user == null)
            {
                return string.Format(DefaultLanguage.UserIdText, member.UserId);
            }

            var displayName = Models.Contact.ResolveDisplayName(string.Empty, user.Username, user.FirstName, user.LastName);
            return string.IsNullOrWhiteSpace(displayName)
                ? string.Format(DefaultLanguage.UserIdText, member.UserId)
                : displayName;
        }

        private static string GetSimpleRoleDisplay(int role)
        {
            return role == 1 ? DefaultLanguage.OwnerBadge : DefaultLanguage.MemberRole;
        }

        private async Task ShowMemberOptionsAsync(ChatMemberDto member, UserDto user)
        {
            try
            {
                var userName = ResolveMemberDisplayName(member, user);
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

                var contact = CreateDirectContactFromUser(userId, user);
                var chatResult = await App.ApiService.CreateOrGetPrivateChatAsync(userId);
                if (chatResult == null)
                {
                    MessageBox.ShowError(DefaultLanguage.Error, DefaultLanguage.ErrorTitle);
                    return;
                }

                await _chatController.SwitchToChatPublicAsync(contact, chatResult.Id);
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Opened private chat with {ResolveMemberDisplayName(new ChatMemberDto { UserId = userId }, user)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP INFO] Error opening chat: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private Models.Contact CreateDirectContactFromUser(int userId, UserDto user)
        {
            if (user != null)
            {
                return Models.Contact.FromUserDto(user);
            }

            return new Models.Contact(
                userId,
                string.Format(DefaultLanguage.UserIdText, userId),
                string.Empty,
                string.Empty,
                Models.Contact.DefaultAvatarPath);
        }

        private void CloseGroupInfo()
        {
            _ = CloseGroupInfoAsync();
        }

        private async Task CloseGroupInfoAsync()
        {
            isGroupInfoOpen = false;
            await PageMainInfoPanelHelper.HideAsync(GroupInfoPanel, GroupInfoOverlay, GroupInfoTransform, Dispatcher);
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
