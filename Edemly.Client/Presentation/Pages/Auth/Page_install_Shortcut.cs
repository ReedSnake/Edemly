#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Services;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class Page_install
    {
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new Page_login());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAGE_INSTALL] Cancel navigation failed: {ex}");

                try
                {
                    if (NavigationService?.CanGoBack == true)
                    {
                        NavigationService.GoBack();
                    }
                }
                catch (Exception goBackEx)
                {
                    Debug.WriteLine($"[PAGE_INSTALL] GoBack failed: {goBackEx}");
                }
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedLanguage = GetSelectedLanguageTag();
                SaveLanguageSelection(selectedLanguage);
                TryLoadLanguage(selectedLanguage, "[PAGE_INSTALL] LoadLanguage on continue failed");
                ApplyLanguage();

                var selectedCompanyTag = GetSelectedCompanyTag();
                App.SetCompanyAndApply(selectedCompanyTag, markInstalled: true);

                if (DesktopShortcutCheckBox.IsChecked == true)
                {
                    ConfigService.Instance.CreateDesktopShortcut = true;

                    var created = _desktopShortcutService.TryCreateOrReplaceShortcut(
                        ShortcutFileName,
                        ConfigService.Instance?.ExePath,
                        BuildShortcutArgument(selectedCompanyTag));

                    if (!created)
                    {
                        MessageBox.Show(
                            DefaultLanguage.ShortcutCreateFailed,
                            "Edemly",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    ConfigService.Instance.CreateDesktopShortcut = false;
                }

                await Task.Delay(80);
                NavigationService?.Navigate(new Page_login());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error: " + ex.Message,
                    "Edemly",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string BuildShortcutArgument(string selectedCompanyTag)
        {
            var baseUrl = App.BaseServerUrlNoCompany?.TrimEnd('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(selectedCompanyTag)
                ? baseUrl
                : $"{baseUrl}/{selectedCompanyTag.Trim().Trim('/')}";
        }
    }
}
