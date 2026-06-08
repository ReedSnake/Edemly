#nullable disable

using Edemly.Client.Application.Localization;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class GroupSettingsPage
    {
        private void GroupNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonVisibility();
        }

        private void GroupDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSaveButtonVisibility();
        }

        private void UpdateSaveButtonVisibility()
        {
            try
            {
                if (!_isOwner)
                {
                    SaveButton.Visibility = Visibility.Collapsed;
                    HeaderSaveButton.Visibility = Visibility.Collapsed;
                    return;
                }

                var currentName = GroupNameTextBox.Text?.Trim() ?? string.Empty;
                var currentDescription = GroupDescriptionTextBox.Text?.Trim() ?? string.Empty;

                var hasTextChanges = currentName != _originalGroupName
                    || currentDescription != _originalGroupDescription;

                var hasChanges = hasTextChanges || _iconChanged;
                SaveButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
                HeaderSaveButton.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error updating save button visibility: {ex.Message}");
            }
        }

        private void ChangeIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOwner)
            {
                MessageBox.Show(DefaultLanguage.OwnerOnlyChangeIcon, DefaultLanguage.PermissionDenied,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                FilterIndex = 1,
                Title = DefaultLanguage.SelectGroupIcon
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _pendingIconPath = openFileDialog.FileName;
                _iconChanged = true;

                ShowIconPreview(_pendingIconPath);
                UpdateSaveButtonVisibility();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(DefaultLanguage.ErrorText, ex.Message), DefaultLanguage.ErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                _pendingIconPath = null;
                _iconChanged = false;
            }
        }

        private void ShowIconPreview(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                GroupIconImage.ImageSource = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Error showing preview: {ex.Message}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isOwner)
                {
                    MessageBox.Show(DefaultLanguage.OwnerOnlyChangeSettings, DefaultLanguage.PermissionDenied,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newName = GroupNameTextBox.Text?.Trim();
                var newDescription = GroupDescriptionTextBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show(DefaultLanguage.GroupNameEmpty, "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveButton.IsEnabled = false;
                HeaderSaveButton.IsEnabled = false;

                var finalIconUrl = await TryUploadPendingIconAsync(_originalIconUrl);
                var result = await _apiClient.Chats.UpdateChatAsync(_chatId, name: newName, description: newDescription);

                if (!result.Success)
                {
                    MessageBox.Show(string.Format(DefaultLanguage.FailedUpdate, result.Error),
                        DefaultLanguage.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await ApplySavedGroupStateAsync(newName, newDescription, finalIconUrl);

                MessageBox.Show(DefaultLanguage.GroupSettingsUpdated, DefaultLanguage.SuccessTitle,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GROUP SETTINGS] Exception: {ex.Message}");
                MessageBox.Show(string.Format(DefaultLanguage.ErrorText, ex.Message),
                    DefaultLanguage.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
                HeaderSaveButton.IsEnabled = true;
            }
        }

        private async Task<string> TryUploadPendingIconAsync(string currentIconUrl)
        {
            if (!_iconChanged || string.IsNullOrWhiteSpace(_pendingIconPath) || !File.Exists(_pendingIconPath))
            {
                return currentIconUrl;
            }

            ChangeIconButton.IsEnabled = false;

            try
            {
                var uploadResult = await _apiClient.Files.UploadGroupIconAsync(_chatId, _pendingIconPath);
                if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Url))
                {
                    MessageBox.Show(string.Format(DefaultLanguage.IconUploadFailed, uploadResult.Error),
                        DefaultLanguage.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return currentIconUrl;
                }

                if (!string.IsNullOrWhiteSpace(_originalIconUrl))
                {
                    try
                    {
                        App.GlobalProfilePictureCache.InvalidateCache(_originalIconUrl);
                    }
                    catch
                    {
                    }
                }

                _groupContact.PhotoPath = uploadResult.Url;

                try
                {
                    await App.GlobalProfilePictureCache.ForceDownloadAsync(uploadResult.Url);
                }
                catch
                {
                }

                App.GlobalChatController?.UpdateGroupIcon(_chatId, uploadResult.Url);
                return uploadResult.Url;
            }
            finally
            {
                ChangeIconButton.IsEnabled = true;
            }
        }

        private async Task ApplySavedGroupStateAsync(string newName, string newDescription, string finalIconUrl)
        {
            _originalGroupName = newName ?? string.Empty;
            _originalGroupDescription = newDescription ?? string.Empty;
            _originalIconUrl = finalIconUrl;

            _iconChanged = false;
            _pendingIconPath = null;

            _groupContact.Name = newName;

            if (App.GlobalChatController != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    App.GlobalChatController.UpdateChatButtonName(_chatId, newName);
                });
            }

            if (App.HubService != null)
            {
                await App.HubService.NotifyGroupUpdateAsync(_chatId, newName, newDescription, finalIconUrl);
            }

            SaveButton.Visibility = Visibility.Collapsed;
            HeaderSaveButton.Visibility = Visibility.Collapsed;
        }
    }
}
