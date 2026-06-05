#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Profiles;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private async Task LoadUserDataAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            try
            {
                var userInfo = await _apiService.GetUserInfoAsync();
                if (userInfo == null)
                {
                    return;
                }

                var snapshot = UserProfileSnapshot.From(userInfo);

                FirstNameTextBox.Text = snapshot.FirstName;
                LastNameTextBox.Text = snapshot.LastName;
                UsernameTextBox.Text = snapshot.Username;
                EmailTextBox.Text = snapshot.Email;
                PhoneNumberTextBox.Text = snapshot.PhoneNumber;
                AboutTextBox.Text = snapshot.Description;

                _originalProfile = snapshot;
                _currentAvatarPath = snapshot.PfpUrl;

                App.CurrentUserEmail = snapshot.Email;
                App.CurrentUsername = snapshot.Username;
                App.CurrentUserName = ProfileInputValidator.BuildDisplayName(snapshot.FirstName, snapshot.LastName, snapshot.Username);
                App.CurrentUserPhotoUrl = snapshot.PfpUrl;

                UpdateInitials();

                if (!string.IsNullOrWhiteSpace(snapshot.PfpUrl))
                {
                    await LoadAvatarFromUrlAsync(snapshot.PfpUrl);
                }
                else
                {
                    ShowInitials();
                }

                CheckForChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] LoadUserDataAsync error: {ex.Message}");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _ = SaveUserDataAsync();
        }

        private async Task SaveUserDataAsync()
        {
            try
            {
                if (!TryBuildValidatedRequest(out var request))
                {
                    return;
                }

                var (success, error) = await _apiService.UpdateUserInfoAsync(request);
                if (!success)
                {
                    MessageBox.ShowError(error ?? DefaultLanguage.FailedSaveUserSettings, DefaultLanguage.ErrorTitle);
                    return;
                }

                UpdateSavedProfile(request);
                App.CurrentUsername = request.Username;
                App.CurrentUserName = ProfileInputValidator.BuildDisplayName(request.FirstName, request.LastName, request.Username);
                App.CurrentUserPhotoUrl = request.PfpUrl;

                MessageBox.Show(DefaultLanguage.SettingsSaved, DefaultLanguage.SuccessTitle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] SaveUserDataAsync error: {ex.Message}");
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorSavingSettings, ex.Message), DefaultLanguage.ErrorTitle);
            }
        }

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
                avatarPathOverride ?? _currentAvatarPath ?? string.Empty);
        }

        private void UpdateSavedProfile(UserProfileUpdateRequest request)
        {
            _originalProfile = UserProfileSnapshot.From(request, EmailTextBox.Text);
            _currentAvatarPath = _originalProfile.PfpUrl;
            CheckForChanges();
        }

        private void PhoneNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckForChanges();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInitials();
            CheckForChanges();
        }

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInitials();
            CheckForChanges();
        }

        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateInitials();
            CheckForChanges();
        }

        private void AboutTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckForChanges();
        }

        private void CheckForChanges()
        {
            try
            {
                var request = CreateProfileUpdateRequest();
                _hasUnsavedChanges = !_originalProfile.Matches(request);
                SaveButton.Visibility = _hasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] CheckForChanges failed: {ex}");
            }
        }
    }
}
