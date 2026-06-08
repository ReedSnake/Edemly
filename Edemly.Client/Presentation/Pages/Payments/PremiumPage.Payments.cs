#nullable enable

using Edemly.Client.Api;
using System.IO;
using System.Text;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Payments
{
    public partial class PremiumPage
    {
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MainPage());
        }

        private async void MonthlyButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPaymentFlowAsync(MonthlyAmount);
        }

        private async void YearlyButton_Click(object sender, RoutedEventArgs e)
        {
            await StartPaymentFlowAsync(YearlyAmount);
        }

        private async Task StartPaymentFlowAsync(decimal amount)
        {
            try
            {
                if (App.ApiClients is not ApiClients _apiClient)
                {
                    MessageBox.ShowError(DefaultLanguage.PremiumApiError, DefaultLanguage.PremiumPaymentError);
                    return;
                }

                var response = await _apiClient.Payments.InitiatePaymentAsync(amount);
                if (!response.Success || string.IsNullOrEmpty(response.Html))
                {
                    MessageBox.ShowError(response.Error ?? DefaultLanguage.PremiumPaymentFailed, DefaultLanguage.PremiumPaymentError);
                    return;
                }

                var tempHtmlPath = await SavePaymentHtmlAsync(response.Html);

                try
                {
                    _externalNavigationLauncher.OpenFile(tempHtmlPath);
                }
                catch (Exception ex)
                {
                    MessageBox.ShowError(string.Format(DefaultLanguage.PremiumOpenPageError, ex.Message), DefaultLanguage.PremiumPaymentError);
                    System.Diagnostics.Debug.WriteLine($"[PAGE_PREMIUM] Failed to open payment HTML: {ex}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError("Error: " + ex.Message, DefaultLanguage.PremiumPaymentError);
            }
        }

        private static async Task<string> SavePaymentHtmlAsync(string html)
        {
            var path = Path.Combine(Path.GetTempPath(), $"edemly_payment_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(path, html, Encoding.UTF8);
            return path;
        }
    }
}
