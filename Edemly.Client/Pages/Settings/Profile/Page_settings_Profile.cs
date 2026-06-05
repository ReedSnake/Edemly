#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Users.Profile;
using System.Windows;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private async Task LoadUserDataAsync()
        {
            if (!_profileState.TryBeginInitialization())
            {
                return;
            }

            try
            {
                var userInfo = await _apiService.GetUserInfoAsync();
                if (userInfo == null)
                {
                    return;
                }

                var snapshot = UserProfileSnapshot.From(userInfo);

                ApplyProfileSnapshot(snapshot);
                SyncCurrentUserProfile(snapshot);
                _profileState.Load(snapshot);

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
                SyncCurrentUserProfile(UserProfileSnapshot.From(request, EmailTextBox.Text));

                MessageBox.Show(DefaultLanguage.SettingsSaved, DefaultLanguage.SuccessTitle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] SaveUserDataAsync error: {ex.Message}");
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorSavingSettings, ex.Message), DefaultLanguage.ErrorTitle);
            }
        }
    }
}
