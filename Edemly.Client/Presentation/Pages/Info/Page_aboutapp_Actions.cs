#nullable enable

using System.Windows;
using System.Windows.Navigation;

namespace Edemly.Client.Presentation.Pages.Info
{
    public partial class Page_aboutapp
    {
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                _externalNavigationLauncher.OpenUri(e.Uri);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ABOUT] Failed to open support link: {ex.Message}");
            }
        }
    }
}
