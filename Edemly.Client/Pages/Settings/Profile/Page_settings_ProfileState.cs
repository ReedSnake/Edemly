#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Users.Profile;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private bool TryBuildValidatedRequest(out UserProfileUpdateRequest request)
        {
            request = CreateProfileUpdateRequest();

            if (ProfileInputValidator.TryValidate(request, out var errorMessage))
            {
                return true;
            }

            MessageBox.ShowWarning(errorMessage, DefaultLanguage.WarningTitle);
            return false;
        }

        private UserProfileUpdateRequest CreateProfileUpdateRequest(string? avatarPathOverride = null)
        {
            return new UserProfileUpdateRequest(
                UsernameTextBox.Text ?? string.Empty,
                FirstNameTextBox.Text ?? string.Empty,
                LastNameTextBox.Text ?? string.Empty,
                PhoneNumberTextBox.Text ?? string.Empty,
                AboutTextBox.Text ?? string.Empty,
                avatarPathOverride ?? _profileState.CurrentAvatarPath);
        }

        private void UpdateSavedProfile(UserProfileUpdateRequest request)
        {
            _profileState.MarkSaved(request, EmailTextBox.Text);
            CheckForChanges();
        }

        private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleProfileFieldChanged();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleProfileFieldChanged(refreshInitials: true);
        }

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleProfileFieldChanged(refreshInitials: true);
        }

        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleProfileFieldChanged(refreshInitials: true);
        }

        private void AboutTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            HandleProfileFieldChanged();
        }

        private void HandleProfileFieldChanged(bool refreshInitials = false)
        {
            if (refreshInitials)
            {
                UpdateInitials();
            }

            CheckForChanges();
        }

        private void CheckForChanges()
        {
            try
            {
                var request = CreateProfileUpdateRequest();
                var hasChanges = _profileState.UpdateHasChanges(request);
                SaveButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] CheckForChanges failed: {ex}");
            }
        }
    }
}
