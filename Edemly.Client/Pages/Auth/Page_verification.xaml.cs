using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Edemly.Contracts.Auth;
using MessageBox = Edemly.Client.Pages.MessageBox;
using Edemly.Client.Lang;
using Edemly.Client.Services;

namespace Edemly.Client
{
    /// <summary>
    /// Логика взаємодії для Page_verification.xaml
    /// </summary>
    public partial class Page_verification : Page
    {
        private readonly string _userEmail;
        private readonly bool _isRegistration;
        private readonly string _username;
        private readonly bool _rememberMe;

        public Page_verification(string email = "", bool isRegistration = false, string username = "", bool rememberMe = true)
        {
            InitializeComponent();
            _userEmail = email;
            _isRegistration = isRegistration;
            _username = username;
            _rememberMe = rememberMe;

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            CodeTextBox.Focus();
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine("[PAGE_VERIFICATION] Theme changed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_VERIFICATION] OnThemeChanged error: {ex}");
            }
        }

        private void ApplyThemeToPage()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                var grid = this.Content as Grid;
                if (grid != null)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop(palette.BackgroundDark, 0.0));
                    gradientBrush.GradientStops.Add(new GradientStop(palette.Secondary, 0.5));
                    grid.Background = gradientBrush;
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_VERIFICATION] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_VERIFICATION] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterValidCode, DefaultLanguage.ErrorTitle);
                return;
            }

            VerifyButton.IsEnabled = false;
            VerifyButton.Content = DefaultLanguage.Verifying;

            try
            {
                AuthResponseDto? authResponse = null;

                if (_isRegistration)
                {
                    App.AuthService.ClearAuthData();

                    authResponse = await App.AuthService.RegisterWithCodeAsync(_userEmail, code, _username);

                    if (authResponse == null)
                    {
                        MessageBox.ShowError(DefaultLanguage.RegistrationFailedMessage, DefaultLanguage.ErrorTitle);
                        return;
                    }

                    if (!_rememberMe)
                    {
                        App.AuthService.ClearAuthData();
                    }
                }
                else
                {
                    App.AuthService.ClearAuthData();

                    authResponse = await App.AuthService.LoginWithCodeAsync(_userEmail, code);

                    if (authResponse == null)
                    {
                        MessageBox.ShowError(DefaultLanguage.LoginFailedMessage, DefaultLanguage.ErrorTitle);
                        return;
                    }

                    if (!_rememberMe)
                    {
                        App.AuthService.ClearAuthData();
                    }
                }

                App.SetCurrentUser(
                    authResponse.UserId,
                    authResponse.Email,
                    authResponse.Username,
                    null,
                    authResponse.Token
                );

                App.ApiService.SetAuthToken(authResponse.Token);
                await App.RefreshCurrentUserProfileAsync();

                await App.HubService.ConnectAsync(authResponse.Token);

                NavigationService.Navigate(new Page_main());
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"Error: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
            finally
            {
                VerifyButton.IsEnabled = true;
                VerifyButton.Content = DefaultLanguage.VerifyButton;
            }
        }

        private async void ResendText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(_userEmail))
                return;

            try
            {
                bool success = await App.AuthService.SendVerificationCodeAsync(_userEmail);

                if (success)
                {
                    MessageBox.ShowInfo(DefaultLanguage.VerificationResent, DefaultLanguage.SuccessTitle);
                    CodeTextBox.Text = "";
                    CodeTextBox.Focus();
                }
                else
                {
                    MessageBox.ShowError(DefaultLanguage.FailedResendCode, DefaultLanguage.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"Error: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        private void BackToLoginText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Page_login());
        }

        private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                VerifyButton_Click(sender, e);
            }
        }
    }
}
