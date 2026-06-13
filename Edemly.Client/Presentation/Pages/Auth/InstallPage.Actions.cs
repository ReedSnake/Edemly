#nullable enable

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
    }
}
