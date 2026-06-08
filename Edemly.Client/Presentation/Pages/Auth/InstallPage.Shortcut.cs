#nullable enable

using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class InstallPage
    {
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new LoginPage());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstallPage] Cancel navigation failed: {ex}");

                try
                {
                    if (NavigationService?.CanGoBack == true)
                    {
                        NavigationService.GoBack();
                    }
                }
                catch (Exception goBackEx)
                {
                    Debug.WriteLine($"[InstallPage] GoBack failed: {goBackEx}");
                }
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedLanguage = GetSelectedLanguageTag();
                SaveLanguageSelection(selectedLanguage);
                TryLoadLanguage(selectedLanguage, "[InstallPage] LoadLanguage on continue failed");
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
                NavigationService?.Navigate(new LoginPage());
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
