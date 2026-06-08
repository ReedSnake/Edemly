#nullable enable

using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private void SetThemeResource(FrameworkElement element, DependencyProperty property, string resourceKey)
        {
            element?.SetResourceReference(property, resourceKey);
        }

        private void ApplyTextInputPlaceholderStyle(TextBox textBox, string placeholder)
        {
            if (textBox == null)
            {
                return;
            }

            textBox.Text = placeholder;
            SetThemeResource(textBox, Control.ForegroundProperty, "ThemeDisabledTextBrush");
            textBox.FontStyle = FontStyles.Italic;
        }

        private void ApplyTextInputActiveStyle(TextBox textBox, string? text = null)
        {
            if (textBox == null)
            {
                return;
            }

            if (text != null)
            {
                textBox.Text = text;
            }

            SetThemeResource(textBox, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
            textBox.FontStyle = FontStyles.Normal;
        }

        private void ApplyLocalization()
        {
            try
            {
                ApplyStaticButtonGlyphs();

                var myPlannerText = FindName("MyPlannerButtonText") as TextBlock;
                if (myPlannerText != null) myPlannerText.Text = DefaultLanguage.MyPlanner;

                var chatsHeaderText = FindName("ChatsHeaderText") as TextBlock;
                if (chatsHeaderText != null) chatsHeaderText.Text = DefaultLanguage.Chats;

                if (CreateGroupButton != null) CreateGroupButton.ToolTip = DefaultLanguage.CreateGroupTooltip;

                var menuTitleText = FindName("MenuTitleText") as TextBlock;
                if (menuTitleText != null) menuTitleText.Text = DefaultLanguage.Menu;
                if (SettingsButton != null) SettingsButton.Content = DefaultLanguage.MenuSettings;
                if (PremiumButton != null) PremiumButton.Content = DefaultLanguage.MenuPremium;
                if (HelpButton != null) HelpButton.Content = DefaultLanguage.MenuAbout;
                if (LogoutButton != null) LogoutButton.Content = DefaultLanguage.MenuLogout;

                var contactInfoTitle = FindName("ContactInfoTitle") as TextBlock;
                if (contactInfoTitle != null) contactInfoTitle.Text = DefaultLanguage.ContactInfo;
                var displayNameLabelText = FindName("DisplayNameLabelText") as TextBlock;
                if (displayNameLabelText != null) displayNameLabelText.Text = DefaultLanguage.NameLabel;
                var usernameLabelText = FindName("UsernameLabelText") as TextBlock;
                if (usernameLabelText != null) usernameLabelText.Text = DefaultLanguage.UserNameLabel;
                var firstNameLabelText = FindName("FirstNameLabelText") as TextBlock;
                if (firstNameLabelText != null) firstNameLabelText.Text = DefaultLanguage.SettingsFirstNameLabel;
                var lastNameLabelText = FindName("LastNameLabelText") as TextBlock;
                if (lastNameLabelText != null) lastNameLabelText.Text = DefaultLanguage.SettingsLastNameLabel;
                var emailLabelText = FindName("EmailLabelText") as TextBlock;
                if (emailLabelText != null) emailLabelText.Text = DefaultLanguage.EmailLabel;
                var phoneLabelText = FindName("PhoneLabelText") as TextBlock;
                if (phoneLabelText != null) phoneLabelText.Text = DefaultLanguage.PhoneLabel;
                var personalNotesTitle = FindName("PersonalNotesTitle") as TextBlock;
                if (personalNotesTitle != null) personalNotesTitle.Text = DefaultLanguage.PersonalNotes;
                var personalNotesPrivate = FindName("PersonalNotesPrivate") as TextBlock;
                if (personalNotesPrivate != null) personalNotesPrivate.Text = DefaultLanguage.NotesPrivate;
                if (NoNotesText != null) NoNotesText.Text = DefaultLanguage.NoNotes;
                if (SaveContactNoteButton != null) SaveContactNoteButton.Content = DefaultLanguage.ContactAddNoteButton;
                if (DeleteContactNoteButton != null) DeleteContactNoteButton.Content = DefaultLanguage.ContactDeleteNoteButton;
                if (EditContactButton != null) EditContactButton.ToolTip = DefaultLanguage.ContactUpdateNoteButton;
                var closeContactInfoButton = FindName("CloseContactInfoButton") as Button;
                if (closeContactInfoButton != null) closeContactInfoButton.Content = DefaultLanguage.Close;

                var groupInfoTitle = FindName("GroupInfoTitle") as TextBlock;
                if (groupInfoTitle != null) groupInfoTitle.Text = DefaultLanguage.GroupInfo;
                var groupNameLabelText = FindName("GroupNameLabelText") as TextBlock;
                if (groupNameLabelText != null) groupNameLabelText.Text = DefaultLanguage.GroupNameLabel;
                var descriptionLabelText = FindName("DescriptionLabelText") as TextBlock;
                if (descriptionLabelText != null) descriptionLabelText.Text = DefaultLanguage.DescriptionLabel;
                if (NoDescriptionText != null) NoDescriptionText.Text = DefaultLanguage.NoDescription;
                var membersLabelText = FindName("MembersLabelText") as TextBlock;
                if (membersLabelText != null) membersLabelText.Text = DefaultLanguage.MembersLabel;
                if (CloseGroupInfoButton != null) CloseGroupInfoButton.Content = DefaultLanguage.Close;
                if (GroupSettingsIconButton != null) GroupSettingsIconButton.ToolTip = DefaultLanguage.GroupSettings;

                var stickersTitle = FindName("StickersTitle") as TextBlock;
                if (stickersTitle != null) stickersTitle.Text = DefaultLanguage.Stickers;

                var searchResultsTitle = FindName("SearchResultsTitle") as TextBlock;
                if (searchResultsTitle != null) searchResultsTitle.Text = DefaultLanguage.SearchResults;

                var createGroupTitle = FindName("CreateGroupTitle") as TextBlock;
                if (createGroupTitle != null) createGroupTitle.Text = DefaultLanguage.CreateNewGroup;
                var groupNameInputLabel = FindName("GroupNameInputLabel") as TextBlock;
                if (groupNameInputLabel != null) groupNameInputLabel.Text = DefaultLanguage.GroupNameLabel;
                var addParticipantsLabel = FindName("AddParticipantsLabel") as TextBlock;
                if (addParticipantsLabel != null) addParticipantsLabel.Text = DefaultLanguage.AddParticipants;
                var cancelCreateGroupButton = FindName("CancelCreateGroupButton") as Button;
                if (cancelCreateGroupButton != null) cancelCreateGroupButton.Content = DefaultLanguage.Cancel;
                if (ConfirmCreateGroupButton != null) ConfirmCreateGroupButton.Content = DefaultLanguage.CreateGroup;

                if (CallButton != null) CallButton.ToolTip = DefaultLanguage.CallTooltip;
                if (AttachFileButton != null) AttachFileButton.ToolTip = DefaultLanguage.AttachFile;
                if (StickerButton != null) StickerButton.ToolTip = DefaultLanguage.SendSticker;

                if (SearchTextBox != null && IsSearchPlaceholderText(SearchTextBox.Text))
                {
                    ApplyTextInputPlaceholderStyle(SearchTextBox, DefaultLanguage.SearchPlaceholder);
                }

                if (MessageTextBox != null && (string.IsNullOrWhiteSpace(MessageTextBox.Text) || MainPageInputHelper.IsPlaceholderText(MessageTextBox.Text)))
                {
                    SetMessagePlaceholder();
                }

                UpdateParticipantCountText();

                System.Diagnostics.Debug.WriteLine("[PAGE_MAIN] Localization applied successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] Error applying localization: {ex.Message}");
            }
        }

        private void ApplyStaticButtonGlyphs()
        {
            if (MenuButton != null) MenuButton.Content = MainPageGlyphs.Menu;
            if (CallButton != null) CallButton.Content = MainPageGlyphs.Call;
            if (ContactMenuButton != null) ContactMenuButton.Content = MainPageGlyphs.More;
            if (CloseStickersButton != null) CloseStickersButton.Content = MainPageGlyphs.Close;
            if (GroupSettingsIconButton != null) GroupSettingsIconButton.Content = MainPageGlyphs.Settings;
        }

        private void UpdateParticipantCountText()
        {
            if (ParticipantCountText != null)
            {
                ParticipantCountText.Text = string.Format(DefaultLanguage.ParticipantsSelected, _selectedParticipants.Count);
            }
        }

        private void RefreshPlaceholders()
        {
            try
            {
                if (MessageTextBox != null)
                {
                    if (_isRecording)
                    {
                        ApplyTextInputPlaceholderStyle(MessageTextBox, DefaultLanguage.Loading);
                    }
                    else
                    {
                        var currentText = MessageTextBox.Text?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(currentText) || MainPageInputHelper.IsPlaceholderText(currentText))
                        {
                            SetMessagePlaceholder();
                        }
                    }
                }

                if (SearchTextBox != null && IsSearchPlaceholderText(SearchTextBox.Text))
                {
                    ApplyTextInputPlaceholderStyle(SearchTextBox, DefaultLanguage.SearchPlaceholder);
                }

                if (ParticipantSearchTextBox != null && IsParticipantSearchPlaceholderText(ParticipantSearchTextBox.Text))
                {
                    ApplyTextInputPlaceholderStyle(ParticipantSearchTextBox, DefaultLanguage.SearchUsers);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_MAIN] RefreshPlaceholders error: {ex.Message}");
            }
        }

        private static bool IsSearchPlaceholderText(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                || string.Equals(text.Trim(), DefaultLanguage.SearchPlaceholder, StringComparison.Ordinal)
                || string.Equals(text.Trim(), "Search...", StringComparison.Ordinal);
        }

        private static bool IsParticipantSearchPlaceholderText(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                || string.Equals(text.Trim(), DefaultLanguage.SearchUsers, StringComparison.Ordinal)
                || string.Equals(text.Trim(), "Search users...", StringComparison.Ordinal);
        }
    }
}
