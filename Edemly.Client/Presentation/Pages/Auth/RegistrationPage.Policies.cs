#nullable enable

using System.Windows;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class RegistrationPage
    {
        private void OpenPolicies_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAndShowPoliciesAsync();
        }

        private void ClosePolicies_Click(object sender, RoutedEventArgs e)
        {
            PoliciesPanel.Visibility = Visibility.Collapsed;
        }

        private void AcceptPolicies_Click(object sender, RoutedEventArgs e)
        {
            TermsCheckBox.IsChecked = true;
            PoliciesPanel.Visibility = Visibility.Collapsed;
        }

        private async Task LoadAndShowPoliciesAsync()
        {
            try
            {
                PoliciesContentText.Text = await _legalDocumentLoader.LoadPoliciesAsync();
            }
            catch (Exception ex)
            {
                PoliciesContentText.Text = "Failed to load policies: " + ex.Message;
            }

            PoliciesPanel.Visibility = Visibility.Visible;
        }
    }
}
