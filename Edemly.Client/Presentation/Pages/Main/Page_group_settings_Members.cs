#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_group_settings
    {
        private async Task LoadGroupMembersAsync()
        {
            try
            {
                GroupMembersPanel.Children.Clear();
                GroupMembersPanel.Children.Add(GroupSettingsMemberItemFactory.CreateCenteredStatusText(DefaultLanguage.LoadingMembers));

                _groupMembers = await _apiClient.Chats.GetChatMembersAsync(_chatId) ?? new List<ChatMemberDto>();

                var isOwner = _groupMembers.Any(member => member.UserId == App.CurrentUserId && member.Role == 1);
                ApplyOwnerEditingState(isOwner);

                if (_groupMembers.Count == 0)
                {
                    GroupMembersPanel.Children.Clear();
                    GroupMembersPanel.Children.Add(GroupSettingsMemberItemFactory.CreateCenteredStatusText(DefaultLanguage.NoMembers));
                    return;
                }

                GroupMembersPanel.Children.Clear();

                foreach (var member in _groupMembers)
                {
                    var itemView = GroupSettingsMemberItemFactory.Create(member);
                    itemView.Container.MouseLeftButtonDown += async (_, _) => await ShowMemberOptionsAsync(member);

                    GroupMembersPanel.Children.Add(itemView.Container);
                    _ = LoadMemberDetailsAsync(member, itemView);
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

                ApplyOwnerEditingState(false);
                GroupMembersPanel.Children.Clear();
                GroupMembersPanel.Children.Add(
                    GroupSettingsMemberItemFactory.CreateCenteredStatusText(
                        DefaultLanguage.FailedLoadMembers,
                        "ThemeDangerBrush"));
            }
        }

        private async Task LoadMemberDetailsAsync(ChatMemberDto member, GroupSettingsMemberItemView itemView)
        {
            try
            {
                var user = await _apiClient.Users.GetUserByIdAsync(member.UserId);
                if (user == null)
                {
                    return;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    itemView.NameTextBlock.Text = user.Username ?? string.Format(DefaultLanguage.UserIdText, member.UserId);
                    itemView.DetailTextBlock.Text = user.Email ?? DefaultLanguage.MemberRole;
                    itemView.DetailTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                    itemView.DetailTextBlock.FontStyle = FontStyles.Normal;
                });

                if (!string.IsNullOrWhiteSpace(user.PfpUrl))
                {
                    _ = LoadAvatarAsync(itemView.AvatarBorder, user.PfpUrl);
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
                if (bitmap == null)
                {
                    return;
                }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading avatar: {ex.Message}");
            }
        }

        private static Task ShowMemberOptionsAsync(ChatMemberDto member)
        {
            return Task.CompletedTask;
        }

        public Task RefreshMembersListAsync()
        {
            return LoadGroupMembersAsync();
        }
    }
}
