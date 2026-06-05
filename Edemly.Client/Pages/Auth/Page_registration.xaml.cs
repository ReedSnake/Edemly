using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Pages.Auth
{
    public partial class Page_registration : ThemedPage
    {
        private static readonly Regex UsernameRegex = new(
            @"^[a-zA-Zа-яА-ЯіІїЇєЄґҐ0-9 _-]+$",
            RegexOptions.Compiled);

        public Page_registration()
        {
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ExitCompany();

            NavigationService?.Navigate(new Page_install());
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

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

            if (!IsValidUsername(fullName))
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

            if (!IsValidEmail(email))
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
            string username = FullNameTextBox.Text.Trim();

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

            if (!IsValidUsername(username))
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

        private static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return UsernameRegex.IsMatch(username);
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
            NavigationService?.Navigate(new Page_login());
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);

                return address.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            SignUpButton_Click(SignUpButton, e);
        }

        private void OpenPolicies_Click(object sender, RoutedEventArgs e)
        {
            LoadAndShowPolicies();
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

        private async void LoadAndShowPolicies()
        {
            try
            {
                string exeFolder = Path.GetDirectoryName(
                    Assembly.GetEntryAssembly()?.Location
                    ?? AppDomain.CurrentDomain.BaseDirectory)
                    ?? string.Empty;

                string legalDirectory = Path.Combine(exeFolder, "Assets", "Legal");

                string language =
                    System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                string fileName = language is "uk" or "ua"
                    ? "terms_privacy_uk.txt"
                    : "terms_privacy_en.txt";

                string filePath = Path.Combine(legalDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    string projectDirectory =
                        Directory.GetParent(exeFolder)?.Parent?.Parent?.FullName
                        ?? exeFolder;

                    filePath = Path.Combine(
                        projectDirectory,
                        "Assets",
                        "Legal",
                        fileName);
                }

                string content = File.Exists(filePath)
                    ? await File.ReadAllTextAsync(filePath)
                    : DefaultLanguage.PoliciesContent;

                PoliciesContentText.Text = content;
                PoliciesPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                PoliciesContentText.Text = "Failed to load policies: " + ex.Message;
                PoliciesPanel.Visibility = Visibility.Visible;
            }
        }
    }
}