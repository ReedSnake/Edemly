#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Users.Profile;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Edemly.Client.Api;
namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class Page_settings
    {
        private async void OnProfileUpdated(int userId, string newPfpUrl)
        {
            try
            {
                if (App.CurrentUserId != userId)
                {
                    return;
                }

                await RefreshAvatarFromProfileUpdateAsync(newPfpUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnProfileUpdated failed: {ex}");
            }
        }

        private void ChangePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ChangePhotoAsync();
        }

        private async Task ChangePhotoAsync()
        {
            try
            {
                if (!TryBuildValidatedRequest(out _))
                {
                    return;
                }

                var dialog = CreateProfilePhotoDialog();
                if (dialog.ShowDialog() != true || !File.Exists(dialog.FileName))
                {
                    return;
                }

                await PreviewSelectedAvatarAsync(dialog.FileName);

                ChangePhotoButton.IsEnabled = false;
                ChangePhotoButton.Content = DefaultLanguage.Uploading;

                var upload = await _apiClient.Files.UploadProfilePictureAsync(dialog.FileName);
                if (!upload.Success || string.IsNullOrWhiteSpace(upload.Url))
                {
                    MessageBox.ShowError(string.Format(DefaultLanguage.PhotoUploadFailed, upload.Error), DefaultLanguage.ErrorTitle);
                    return;
                }

                var request = CreateProfileUpdateRequest(upload.Url);
                var previousAvatarPath = _profileState.OriginalProfile.PfpUrl;
                var (success, error) = await _apiClient.Users.UpdateUserInfoAsync(request);

                _profileState.SetCurrentAvatar(upload.Url);
                App.CurrentUserPhotoUrl = upload.Url;

                if (!success)
                {
                    CheckForChanges();
                    MessageBox.ShowWarning(error ?? DefaultLanguage.PhotoUploadedButUpdateFailed, DefaultLanguage.WarningTitle);
                    return;
                }

                InvalidatePreviousAvatar(previousAvatarPath);
                await WarmAvatarCacheAsync(upload.Url);

                UpdateSavedProfile(request);
                SyncCurrentUserProfile(UserProfileSnapshot.From(request, EmailTextBox.Text));
                await LoadAvatarFromUrlAsync(upload.Url);
                await NotifyAvatarUpdatedAsync(upload.Url);

                MessageBox.Show(DefaultLanguage.ProfilePhotoUpdated, DefaultLanguage.SuccessTitle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ChangePhotoAsync error: {ex.Message}");
                MessageBox.ShowError(string.Format(DefaultLanguage.ErrorSavingSettings, ex.Message), DefaultLanguage.ErrorTitle);
            }
            finally
            {
                ChangePhotoButton.IsEnabled = true;
                ChangePhotoButton.Content = DefaultLanguage.ChangePhoto;
            }
        }

        private static OpenFileDialog CreateProfilePhotoDialog()
        {
            return new OpenFileDialog
            {
                Title = DefaultLanguage.SelectProfilePhotoTitle,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                Multiselect = false
            };
        }

        private async Task PreviewSelectedAvatarAsync(string filePath)
        {
            var localBitmap = await App.GlobalProfilePictureCache.CacheLocalFileAsync(filePath);
            if (localBitmap != null)
            {
                ShowAvatarImage(localBitmap);
            }
        }

        private void InvalidatePreviousAvatar(string? avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath))
            {
                return;
            }

            try
            {
                App.GlobalProfilePictureCache.InvalidateCache(avatarPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] InvalidateCache failed: {ex}");
            }
        }

        private async Task WarmAvatarCacheAsync(string avatarPath)
        {
            try
            {
                await App.GlobalProfilePictureCache.ForceDownloadAsync(avatarPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ForceDownloadAsync failed: {ex}");
            }
        }

        private async Task NotifyAvatarUpdatedAsync(string avatarPath)
        {
            try
            {
                if (App.CurrentUserId.HasValue)
                {
                    await App.HubService.NotifyProfileUpdateAsync(App.CurrentUserId.Value, avatarPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] NotifyProfileUpdate failed: {ex}");
            }
        }

        private async Task RefreshAvatarFromProfileUpdateAsync(string avatarPath)
        {
            try
            {
                var bitmap = string.IsNullOrWhiteSpace(avatarPath)
                    ? null
                    : await App.GlobalProfilePictureCache.ForceDownloadAsync(avatarPath);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (bitmap != null)
                    {
                        ShowAvatarImage(bitmap);
                    }
                    else
                    {
                        ShowInitials();
                    }

                    _profileState.UpdateAvatar(avatarPath);
                    App.CurrentUserPhotoUrl = _profileState.CurrentAvatarPath;
                    SyncCurrentUserProfile(UserProfileSnapshot.From(CreateProfileUpdateRequest(), EmailTextBox.Text));
                    CheckForChanges();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] RefreshAvatarFromProfileUpdateAsync failed: {ex}");
            }
        }

        private void AvatarBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _ = ChangePhotoAsync();
        }

        private void AvatarImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAvatarClip();
        }

        private void AvatarBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAvatarClip();
        }
    }
}
