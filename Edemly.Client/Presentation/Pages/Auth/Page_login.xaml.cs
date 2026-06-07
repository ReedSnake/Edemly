using Edemly.Client.Application.Auth;
using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class Page_login : ThemedPage
    {
        public Page_login()
        {
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ExitCompany();

            NavigationService?.Navigate(new Page_install());
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterEmail,
                    DefaultLanguage.ErrorTitle);

                return;
            }

            if (!AuthInputValidator.IsValidEmail(email))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterValidEmail,
                    DefaultLanguage.ErrorTitle);

                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = DefaultLanguage.Sending;

            try
            {
                bool success = await App.AuthService.SendVerificationCodeAsync(email);

                if (!success)
                {
                    MessageBox.ShowError(
                        DefaultLanguage.FailedSendVerification,
                        DefaultLanguage.ErrorTitle);

                    return;
                }

                bool rememberMe = RememberMeCheckBox.IsChecked == true;

                NavigationService?.Navigate(
                    new Page_verification(
                        email,
                        isRegistration: false,
                        rememberMe: rememberMe));
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(
                    $"Error: {ex.Message}",
                    DefaultLanguage.ErrorTitle);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = DefaultLanguage.LoginButton;
            }
        }

        private void SignUpText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new Page_registration());
        }

        private void EmailTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            LoginButton_Click(LoginButton, e);
        }

        private void EmailTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
