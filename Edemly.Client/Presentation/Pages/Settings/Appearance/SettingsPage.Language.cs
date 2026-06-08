#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using Edemly.Client.Infrastructure.Storage;
using System.Globalization;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class SettingsPage
    {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] InitializeLanguageControls failed: {ex}");
            }
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Set culture failed: {ex}");
                }

                LanguageService.Instance.LoadLanguage(languageCode);
                InitializeLanguageControls();

                if (NavigationService != null)
                {
                    NavigationService.Navigate(new SettingsPage());
                }
                else
                {
                    System.Windows.Application.Current.MainWindow.Title = DefaultLanguage.AppTitle;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ChangeLanguage failed: {ex}");
            }
        }
    }
}
