#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Users.Profile;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class SettingsPage
    {
        private bool TryBuildValidatedRequest(out UpdateUserDto request)
        {
            request = CreateProfileUpdateRequest();

            if (ProfileInputValidator.TryValidate(request, out var errorMessage))
            {
                return true;
            }

            MessageBox.ShowWarning(errorMessage, DefaultLanguage.WarningTitle);
            return false;
        }

        private UpdateUserDto CreateProfileUpdateRequest(string? avatarPathOverride = null)
        {
            return new UpdateUserDto
            {
                Username = UsernameTextBox.Text?.Trim() ?? string.Empty,
                FirstName = FirstNameTextBox.Text?.Trim() ?? string.Empty,
                LastName = LastNameTextBox.Text?.Trim() ?? string.Empty,
                PhoneNumber = PhoneNumberTextBox.Text?.Trim() ?? string.Empty,
                Description = AboutTextBox.Text?.Trim() ?? string.Empty,
                PfpUrl = (avatarPathOverride ?? _profileState.CurrentAvatarPath)?.Trim()
            };
        }

        private void UpdateSavedProfile(UpdateUserDto request)
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
