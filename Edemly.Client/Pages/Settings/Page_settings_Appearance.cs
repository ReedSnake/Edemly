#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private static readonly Regex PhoneInputRegex = new(@"^[0-9+\-\s()]+$");

        private void InitializeLanguageControls()
        {
            try
            {
                var language = LanguageService.Instance.CurrentLanguage;
                EnglishRadioButton.IsChecked = language == "en";
                UkrainianRadioButton.IsChecked = language == "uk";

                EnglishRadioButton.Content = DefaultLanguage.LanguageEnglishName;
                UkrainianRadioButton.Content = DefaultLanguage.LanguageUkrainianName;
                SelectLanguageLabel.Text = DefaultLanguage.SelectLanguageLabel;
                ThemeSettingsLabel.Text = DefaultLanguage.ThemeSettings;
                ThemeColorLabel.Text = DefaultLanguage.ThemeColor;
                ChangePhotoButton.Content = DefaultLanguage.ChangePhoto;
                SaveButton.Content = DefaultLanguage.SaveButton;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] InitializeLanguageControls failed: {ex}"); }
        }

        private void EnglishRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("en");
        }

        private void UkrainianRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("uk");
        }

        private void ChangeLanguage(string languageCode)
        {
            try
            {
                ConfigService.Instance.Language = languageCode;
                ConfigService.Instance.Save();

                try
                {
                    var culture = languageCode == "uk" ? new CultureInfo("uk-UA") : new CultureInfo("en-US");
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] Set culture failed: {ex}"); }

                LanguageService.Instance.LoadLanguage(languageCode);
                InitializeLanguageControls();

                if (NavigationService != null)
                {
                    NavigationService.Navigate(new Page_settings());
                }
                else
                {
                    System.Windows.Application.Current.MainWindow.Title = DefaultLanguage.AppTitle;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] ChangeLanguage failed: {ex}"); }
        }

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

                    bool isActive = string.Equals(activeTheme, themeName, StringComparison.OrdinalIgnoreCase);
                    preview.BorderThickness = isActive ? new Thickness(3) : new Thickness(1);
                    preview.SetResourceReference(Border.BorderBrushProperty, isActive ? "ThemePrimaryBrush" : "ThemeBorderBrush");
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] UpdateThemePreviewSelection failed: {ex}"); }
        }

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
                System.Diagnostics.Debug.WriteLine($"[PAGE_SETTINGS] SetAppBackgroundImage error: {ex.Message}");
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

        private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !PhoneInputRegex.IsMatch(e.Text);
        }

        private void PhoneNumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = false;
            }
        }

        private void PhoneNumberTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text) || !PhoneInputRegex.IsMatch(text))
            {
                e.CancelCommand();
            }
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
