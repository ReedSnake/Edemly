#nullable disable

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

        private void OpenContactInfo()
        {
            if (_chatController?.CurrentChatContact == null)
            {
                return;
            }

            ShowContactInfoPanel(_chatController.CurrentChatContact);
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
            Interlocked.Increment(ref _contactInfoLoadVersion);
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
