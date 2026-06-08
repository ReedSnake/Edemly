using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Pages.Main;
using Edemly.Contracts.Auth;
using System.Windows;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class VerificationPage : ThemedPage
    {
        private readonly string _userEmail;
        private readonly bool _isRegistration;
        private readonly string _username;
        private readonly bool _rememberMe;

        public VerificationPage(
            string email = "",
            bool isRegistration = false,
            string username = "",
            bool rememberMe = true)
        {
            InitializeComponent();

            _userEmail = email;
            _isRegistration = isRegistration;
            _username = username;
            _rememberMe = rememberMe;

            CodeTextBox.Focus();
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterValidCode,
                    DefaultLanguage.ErrorTitle);

                return;
            }

            VerifyButton.IsEnabled = false;
            VerifyButton.Content = DefaultLanguage.Verifying;

            try
            {
                App.AuthService.ClearAuthData();

                AuthResponseDto? authResponse = _isRegistration
                    ? await App.AuthService.RegisterWithCodeAsync(_userEmail, code, _username)
                    : await App.AuthService.LoginWithCodeAsync(_userEmail, code);

                if (authResponse is null)
                {
                    MessageBox.ShowError(
                        _isRegistration
                            ? DefaultLanguage.RegistrationFailedMessage
                            : DefaultLanguage.LoginFailedMessage,
                        DefaultLanguage.ErrorTitle);

                    return;
                }

                if (!_rememberMe)
                {
                    App.AuthService.ClearAuthData();
                }

                App.SetCurrentUser(
                    authResponse.UserId,
                    authResponse.Email,
                    authResponse.Username,
                    null,
                    authResponse.Token);

                await App.RefreshCurrentUserProfileAsync();

                await App.HubService.ConnectAsync(authResponse.Token);

                NavigationService?.Navigate(new MainPage());
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(
                    $"Error: {ex.Message}",
                    DefaultLanguage.ErrorTitle);
            }
            finally
            {
                VerifyButton.IsEnabled = true;
                VerifyButton.Content = DefaultLanguage.VerifyButton;
            }
        }

        private async void ResendText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_userEmail))
                return;

            try
            {
                bool success = await App.AuthService.SendVerificationCodeAsync(_userEmail);

                if (!success)
                {
                    MessageBox.ShowError(
                        DefaultLanguage.FailedResendCode,
                        DefaultLanguage.ErrorTitle);

                    return;
                }

                MessageBox.ShowInfo(
                    DefaultLanguage.VerificationResent,
                    DefaultLanguage.SuccessTitle);

                CodeTextBox.Text = string.Empty;
                CodeTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.ShowError(
                    $"Error: {ex.Message}",
                    DefaultLanguage.ErrorTitle);
            }
        }

        private void BackToLoginText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage());
        }

        private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            VerifyButton_Click(VerifyButton, e);
        }
    }
}
