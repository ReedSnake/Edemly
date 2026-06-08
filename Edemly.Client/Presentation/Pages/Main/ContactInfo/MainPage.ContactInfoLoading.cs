#nullable disable

using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Threading;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class MainPage
    {
        private void ShowContactInfoPanel(Models.Contact contact)
        {
            isContactInfoOpen = true;

            var requestId = Interlocked.Increment(ref _contactInfoLoadVersion);

            MainPageInfoPanelHelper.PrepareToShow(ContactInfoPanel, ContactInfoOverlay);
            PopulateContactInfo(contact);
            PrepareContactNotesForDisplay(contact.Note);
            ResetContactPhoto();
            EditContactButton.Visibility = Visibility.Visible;
            MainPageInfoPanelHelper.SlideIn(ContactInfoTransform);

            _ = LoadContactPhotoAsync(contact, requestId);
            _ = LoadContactInfoContentAsync(contact, requestId);
        }

        private async Task LoadContactInfoContentAsync(Models.Contact contact, int requestId)
        {
            var initialPhotoPath = NormalizeContactPhotoPath(contact.PhotoPath);

            await RefreshCurrentContactDetailsAsync(contact);
            if (!IsContactInfoRequestCurrent(requestId, contact.UserId))
            {
                return;
            }

            PopulateContactInfo(contact);

            if (!string.Equals(initialPhotoPath, NormalizeContactPhotoPath(contact.PhotoPath), StringComparison.OrdinalIgnoreCase))
            {
                await LoadContactPhotoAsync(contact, requestId);
                if (!IsContactInfoRequestCurrent(requestId, contact.UserId))
                {
                    return;
                }
            }

            await LoadContactNotesAsync(contact, requestId);
        }

        private async Task RefreshCurrentContactDetailsAsync(Models.Contact contact)
        {
            try
            {
                if (_chatController?.IsCurrentChatGroup() == true)
                {
                    return;
                }

                var user = await App.ApiClients.Users.GetUserByIdAsync(contact.UserId);
                contact.ApplyUserProfile(user);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT INFO] Failed to refresh contact details: {ex.Message}");
            }
        }

        private async Task LoadContactPhotoAsync(Models.Contact contact, int requestId)
        {
            var requestedPhotoPath = NormalizeContactPhotoPath(contact.PhotoPath);
            var avatarSource = await MainPageAvatarHelper.ResolveImageSourceAsync(requestedPhotoPath, "[CONTACT INFO]");

            if (!IsContactPhotoRequestCurrent(requestId, contact.UserId, requestedPhotoPath))
            {
                return;
            }

            MainPageAvatarHelper.ApplyImageSource(ContactPhotoBackground, avatarSource);
        }

        private bool IsContactInfoRequestCurrent(int requestId, int userId)
        {
            return isContactInfoOpen
                && requestId == Volatile.Read(ref _contactInfoLoadVersion)
                && _chatController?.CurrentChatContact?.UserId == userId
                && _chatController?.IsCurrentChatGroup() == false;
        }

        private bool IsContactPhotoRequestCurrent(int requestId, int userId, string requestedPhotoPath)
        {
            if (!IsContactInfoRequestCurrent(requestId, userId))
            {
                return false;
            }

            var currentPhotoPath = NormalizeContactPhotoPath(_chatController?.CurrentChatContact?.PhotoPath);
            return string.Equals(currentPhotoPath, requestedPhotoPath, StringComparison.OrdinalIgnoreCase);
        }

        private void ResetContactPhoto()
        {
            MainPageAvatarHelper.ApplyDefaultAvatar(ContactPhotoBackground);
        }

        private static string NormalizeContactPhotoPath(string photoPath)
        {
            return string.IsNullOrWhiteSpace(photoPath)
                ? Models.Contact.DefaultAvatarPath
                : photoPath.Trim();
        }
    }
}
