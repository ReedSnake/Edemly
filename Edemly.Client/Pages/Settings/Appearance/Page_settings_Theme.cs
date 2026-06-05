#nullable enable

using Edemly.Client.Application.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private void ThemeColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
            {
                return;
            }

            var themeName = element.Tag as string;
            if (string.IsNullOrWhiteSpace(themeName))
            {
                return;
            }

            ChangeTheme(themeName);
        }

        private void ChangeTheme(string themeName)
        {
            try
            {
                ThemeService.Instance.SetTheme(themeName);
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Theme changed to: {themeName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ChangeTheme error: {ex}");
            }
        }

        private void UpdateThemePreviewSelection(string activeTheme)
        {
            try
            {
                var themePreviews = new[]
                {
                    (Preview: DefaultThemeColor, ThemeName: "Default"),
                    (Preview: BlueThemeColor, ThemeName: "Blue"),
                    (Preview: PinkThemeColor, ThemeName: "Pink"),
                    (Preview: OrangeThemeColor, ThemeName: "Orange"),
                    (Preview: PurpleThemeColor, ThemeName: "Purple"),
                    (Preview: RedThemeColor, ThemeName: "Red")
                };

                foreach (var (preview, themeName) in themePreviews)
                {
                    if (preview == null)
                    {
                        continue;
                    }

                    var isActive = string.Equals(activeTheme, themeName, StringComparison.OrdinalIgnoreCase);
                    preview.BorderThickness = isActive ? new Thickness(3) : new Thickness(1);
                    preview.SetResourceReference(Border.BorderBrushProperty, isActive ? "ThemePrimaryBrush" : "ThemeBorderBrush");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateThemePreviewSelection failed: {ex}");
            }
        }
    }
}
