#nullable enable

using Edemly.Client.Application.Services;
using Edemly.Client.Infrastructure.Storage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class SettingsPage
    {
        private void SetAppBackgroundImage(string? packUriOrNull)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packUriOrNull))
                {
                    System.Windows.Application.Current.Resources["BackgroundImage"] = null;
                    ConfigService.Instance.BackgroundImagePath = string.Empty;
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(packUriOrNull, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                System.Windows.Application.Current.Resources["BackgroundImage"] = bitmap;
                ConfigService.Instance.BackgroundImagePath = packUriOrNull;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] SetAppBackgroundImage error: {ex.Message}");
                System.Windows.Application.Current.Resources["BackgroundImage"] = null;
                ConfigService.Instance.BackgroundImagePath = string.Empty;
            }
        }

        private void Wallpaper_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            SetAppBackgroundImage(element.Tag as string);
        }

        private void WallpapersScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
