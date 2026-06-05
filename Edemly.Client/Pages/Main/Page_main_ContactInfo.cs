#nullable disable

using Edemly.Client.Application.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            if (_chatController?.CurrentChatContact == null)
            {
                return;
            }

            isContactInfoOpen = true;
            PageMainInfoPanelHelper.PrepareToShow(ContactInfoPanel, ContactInfoOverlay);

            var contact = _chatController.CurrentChatContact;

            await RefreshCurrentContactDetailsAsync(contact);
            PopulateContactInfo(contact);
            await LoadContactPhotoAsync(contact);
            await LoadContactNotesAsync();

            EditContactButton.Visibility = Visibility.Visible;
            PageMainInfoPanelHelper.SlideIn(ContactInfoTransform);
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

        private Task LoadContactPhotoAsync(Models.Contact contact)
        {
            return PageMainAvatarHelper.SetImageSourceAsync(ContactPhotoBackground, contact.PhotoPath, "[CONTACT INFO]");
        }

        private static string GetContactValueOrFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void CloseContactInfo()
        {
            _ = CloseContactInfoAsync();
        }

        private async Task CloseContactInfoAsync()
        {
            isContactInfoOpen = false;
            await PageMainInfoPanelHelper.HideAsync(ContactInfoPanel, ContactInfoOverlay, ContactInfoTransform, Dispatcher);
        }

        private void ContactInfoOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseContactInfo();
        }

        private void CloseContactInfo_Click(object sender, RoutedEventArgs e)
        {
            CloseContactInfo();
        }

        private void ContactInfoText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            if (sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            {
                Clipboard.SetText(textBlock.Text);
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
    }
}
