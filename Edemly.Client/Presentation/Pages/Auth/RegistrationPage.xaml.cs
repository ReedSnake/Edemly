using Edemly.Client.Application.Auth;
using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Legal;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Auth
{
    public partial class RegistrationPage : ThemedPage
    {
        private readonly ILegalDocumentLoader _legalDocumentLoader;

        public RegistrationPage()
            : this(new LegalDocumentLoader())
        {
        }

        internal RegistrationPage(ILegalDocumentLoader legalDocumentLoader)
        {
            _legalDocumentLoader = legalDocumentLoader ?? throw new ArgumentNullException(nameof(legalDocumentLoader));
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ExitCompany();
            NavigationService?.Navigate(new InstallPage());
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            var fullName = FullNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterUsername,
                    DefaultLanguage.ErrorTitle);

                FullNameTextBox.Focus();
                return;
            }

            if (fullName.Length < 3 || fullName.Length > 50)
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.UsernameLength,
                    DefaultLanguage.ErrorTitle);

                FullNameTextBox.Focus();
                return;
            }

            if (!AuthInputValidator.IsValidUsername(fullName))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.UsernameInvalid,
                    DefaultLanguage.ErrorTitle);

                FullNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterEmail,
                    DefaultLanguage.ErrorTitle);

                EmailTextBox.Focus();
                return;
            }

            if (!AuthInputValidator.IsValidEmail(email))
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseEnterValidEmail,
                    DefaultLanguage.ErrorTitle);

                EmailTextBox.Focus();
                return;
            }

            if (TermsCheckBox.IsChecked != true)
            {
                MessageBox.ShowWarning(
                    DefaultLanguage.PleaseAgreeTerms,
                    DefaultLanguage.ErrorTitle);

                return;
            }

            SignUpButton.IsEnabled = false;
            SignUpButton.Content = DefaultLanguage.Sending;

            try
            {
                var success = await App.AuthService.SendVerificationCodeAsync(email);
                if (!success)
                {
                    MessageBox.ShowError(
                        DefaultLanguage.FailedSendVerification,
                        DefaultLanguage.ErrorTitle);

                    return;
                }

                var rememberMe = RememberMeCheckBox.IsChecked == true;
                NavigationService?.Navigate(
                    new VerificationPage(
                        email,
                        isRegistration: true,
                        username: fullName,
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
                SignUpButton.IsEnabled = true;
                SignUpButton.Content = DefaultLanguage.SignUpButton;
            }
        }

        private void FullNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var username = FullNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                ResetUsernameValidationStyle();
                return;
            }

            if (username.Length < 3 || username.Length > 50)
            {
                SetUsernameValidationStyle(
                    isValid: false,
                    tooltip: DefaultLanguage.UsernameLength);

                return;
            }

            if (!AuthInputValidator.IsValidUsername(username))
            {
                SetUsernameValidationStyle(
                    isValid: false,
                    tooltip: DefaultLanguage.UsernameInvalid);

                return;
            }

            SetUsernameValidationStyle(
                isValid: true,
                tooltip: "Valid username");
        }

        private void SetUsernameValidationStyle(bool isValid, string tooltip)
        {
            FullNameTextBox.BorderBrush = isValid
                ? new SolidColorBrush(Color.FromRgb(11, 69, 57))
                : new SolidColorBrush(Color.FromRgb(220, 53, 69));

            FullNameTextBox.ToolTip = tooltip;
        }

        private void ResetUsernameValidationStyle()
        {
            FullNameTextBox.SetResourceReference(
                Control.BorderBrushProperty,
                "ThemeBorderBrush");

            FullNameTextBox.ToolTip = null;
        }

        private void SignInText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage());
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            SignUpButton_Click(SignUpButton, e);
        }
    }
}
