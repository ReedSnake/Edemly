#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Profiles;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Pages.Settings
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

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(newPfpUrl))
                        {
                            var bitmap = await App.GlobalProfilePictureCache.ForceDownloadAsync(newPfpUrl);
                            if (bitmap != null)
                            {
                                AvatarImage.Source = bitmap;
                                AvatarImage.Opacity = 1;
                                AvatarInitials.Visibility = Visibility.Collapsed;
                                UpdateAvatarClip();
                            }
                        }
                        else
                        {
                            ShowInitials();
                        }

                        _currentAvatarPath = newPfpUrl ?? string.Empty;
                        _originalProfile = _originalProfile with { PfpUrl = _currentAvatarPath };
                        App.CurrentUserPhotoUrl = _currentAvatarPath;
                        CheckForChanges();
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnProfileUpdated inner failed: {ex}"); }
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] OnProfileUpdated failed: {ex}"); }
        }

        private async Task LoadAvatarFromUrlAsync(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    ShowInitials();
                    return;
                }

                var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(url);
                if (bitmap == null)
                {
                    ShowInitials();
                    return;
                }

                AvatarImage.Source = bitmap;
                AvatarImage.Opacity = 1;
                AvatarInitials.Visibility = Visibility.Collapsed;
                UpdateAvatarClip();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] LoadAvatarFromUrlAsync error: {ex.Message}");
                ShowInitials();
            }
        }

        private void ShowInitials()
        {
            try
            {
                AvatarImage.Source = null;
                AvatarImage.Opacity = 0;
                AvatarImage.Clip = null;
                AvatarInitials.Text = ProfileInputValidator.BuildInitials(
                    FirstNameTextBox.Text,
                    LastNameTextBox.Text,
                    UsernameTextBox.Text);
                AvatarInitials.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ShowInitials failed: {ex}"); }
        }

        private void UpdateInitials()
        {
            try
            {
                AvatarInitials.Text = ProfileInputValidator.BuildInitials(
                    FirstNameTextBox.Text,
                    LastNameTextBox.Text,
                    UsernameTextBox.Text);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateInitials failed: {ex}"); }
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

                var dialog = new OpenFileDialog
                {
                    Title = DefaultLanguage.SelectProfilePhotoTitle,
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp",
                    Multiselect = false
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                if (!File.Exists(dialog.FileName))
                {
                    return;
                }

                var localBitmap = await App.GlobalProfilePictureCache.CacheLocalFileAsync(dialog.FileName);
                if (localBitmap != null)
                {
                    AvatarImage.Source = localBitmap;
                    AvatarImage.Opacity = 1;
                    AvatarInitials.Visibility = Visibility.Collapsed;
                    UpdateAvatarClip();
                }

                ChangePhotoButton.IsEnabled = false;
                ChangePhotoButton.Content = DefaultLanguage.Uploading;

                var upload = await _apiService.UploadProfilePictureAsync(dialog.FileName);
                if (!upload.Success || string.IsNullOrWhiteSpace(upload.Url))
                {
                    MessageBox.ShowError(string.Format(DefaultLanguage.PhotoUploadFailed, upload.Error), DefaultLanguage.ErrorTitle);
                    return;
                }

                var request = CreateProfileUpdateRequest(upload.Url);
                var previousAvatarPath = _originalProfile.PfpUrl;
                var (success, error) = await _apiService.UpdateUserInfoAsync(request);

                _currentAvatarPath = upload.Url;
                App.CurrentUserPhotoUrl = upload.Url;

                if (!success)
                {
                    CheckForChanges();
                    MessageBox.ShowWarning(error ?? DefaultLanguage.PhotoUploadedButUpdateFailed, DefaultLanguage.WarningTitle);
                    return;
                }

                try { App.GlobalProfilePictureCache.InvalidateCache(previousAvatarPath); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] InvalidateCache failed: {ex}"); }
                try { await App.GlobalProfilePictureCache.ForceDownloadAsync(upload.Url); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ForceDownloadAsync failed: {ex}"); }

                UpdateSavedProfile(request);
                await LoadAvatarFromUrlAsync(upload.Url);

                try
                {
                    if (App.CurrentUserId.HasValue)
                    {
                        await App.HubService.NotifyProfileUpdateAsync(App.CurrentUserId.Value, upload.Url);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] NotifyProfileUpdate failed: {ex}"); }

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

        private void UpdateAvatarClip()
        {
            try
            {
                if (AvatarImage == null || AvatarBorder == null)
                {
                    return;
                }

                double width = AvatarBorder.ActualWidth;
                double height = AvatarBorder.ActualHeight;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                AvatarImage.Clip = new EllipseGeometry(new Point(width / 2.0, height / 2.0), width / 2.0, height / 2.0);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateAvatarClip failed: {ex}"); }
        }
    }
}
