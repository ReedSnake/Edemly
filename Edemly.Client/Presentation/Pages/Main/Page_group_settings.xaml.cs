#nullable disable

using Edemly.Client.Api;
using Edemly.Client.Application.Localization;
using Edemly.Client.Models;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_group_settings : ThemedPage
    {
        private readonly Contact _groupContact;
        private readonly int _chatId;
        private readonly IApiService _apiService;
        private List<ChatMemberDto> _groupMembers = new();

        private bool _isOwner;
        private bool _iconChanged;

        private string _originalGroupName = string.Empty;
        private string _originalGroupDescription = string.Empty;
        private string _originalIconUrl = string.Empty;
        private string _pendingIconPath;

        public Page_group_settings(Contact groupContact, int chatId)
        {
            InitializeComponent();

            _groupContact = groupContact;
            _chatId = chatId;
            _apiService = App.ApiService;

            BackButton.Content = PageNavigationGlyphs.Back;

            GroupNameTextBox.TextChanged += GroupNameTextBox_TextChanged;
            GroupDescriptionTextBox.TextChanged += GroupDescriptionTextBox_TextChanged;

            LoadGroupData();
            _ = LoadGroupMembersAsync();
        }

        protected override void ApplyTheme()
        {
            if (Content is Grid rootGrid)
            {
                rootGrid.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");
            }

            ApplyTextBoxStateResources(GroupNameTextBox);
            ApplyTextBoxStateResources(GroupDescriptionTextBox);
        }

        private void ApplyTextBoxStateResources(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.SetResourceReference(
                Control.BackgroundProperty,
                textBox.IsReadOnly ? "ThemeSurfaceAltBrush" : "ThemeInputBackgroundBrush");
        }

        private void ApplyOwnerEditingState(bool isOwner)
        {
            _isOwner = isOwner;

            ChangeIconButton.Visibility = isOwner ? Visibility.Visible : Visibility.Collapsed;
            ChangeIconButton.IsEnabled = isOwner;
            ChangeIconButton.Opacity = isOwner ? 1.0 : 0.0;
            ChangeIconButton.ToolTip = isOwner ? DefaultLanguage.ChangeIcon : null;

            GroupNameTextBox.IsReadOnly = !isOwner;
            GroupDescriptionTextBox.IsReadOnly = !isOwner;

            ApplyTextBoxStateResources(GroupNameTextBox);
            ApplyTextBoxStateResources(GroupDescriptionTextBox);
            UpdateSaveButtonVisibility();

            System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Is owner: {isOwner}");
        }

        private void LoadGroupData()
        {
            GroupNameTextBox.Text = _groupContact.Name;
            GroupDescriptionTextBox.Text = string.Empty;

            _originalGroupName = _groupContact.Name?.Trim() ?? string.Empty;
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
                if (chatData == null)
                {
                    return;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!string.IsNullOrWhiteSpace(chatData.Description))
                    {
                        GroupDescriptionTextBox.Text = chatData.Description;
                        _originalGroupDescription = chatData.Description.Trim();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error loading group details: {ex.Message}");
            }
        }

        private Task LoadGroupIconAsync()
        {
            return PageMainAvatarHelper.SetImageSourceAsync(GroupIconImage, _groupContact.PhotoPath, "[GROUP SETTINGS]");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
        }
    }
}
