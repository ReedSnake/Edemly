#nullable enable

using Edemly.Client.Application.Users.Profile;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class SettingsPage
    {
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

                ShowAvatarImage(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] LoadAvatarFromUrlAsync error: {ex.Message}");
                ShowInitials();
            }
        }

        private void ShowAvatarImage(BitmapSource bitmap)
        {
            AvatarImage.Source = bitmap;
            AvatarImage.Opacity = 1;
            AvatarInitials.Visibility = Visibility.Collapsed;
            UpdateAvatarClip();
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ShowInitials failed: {ex}");
            }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateInitials failed: {ex}");
            }
        }

        private void UpdateAvatarClip()
        {
            try
            {
                if (AvatarImage == null || AvatarBorder == null)
                {
                    return;
                }

                var width = AvatarBorder.ActualWidth;
                var height = AvatarBorder.ActualHeight;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                AvatarImage.Clip = new EllipseGeometry(new Point(width / 2.0, height / 2.0), width / 2.0, height / 2.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateAvatarClip failed: {ex}");
            }
        }
    }
}
