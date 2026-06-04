using Edemly.Client.Lang;
using Edemly.Client.Services;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using MessageBox = Edemly.Client.Pages.MessageBox;

namespace Edemly.Client
{
    public partial class Page_registration : Page
    {
        private static readonly Regex UsernameRegex = new Regex(@"^[a-zA-Zа-яА-ЯіІїЇєЄґҐ0-9 _-]+$", RegexOptions.Compiled);

        public Page_registration()
        {
            InitializeComponent();

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            var exitButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8),
                Width = 120,
                Height = 25,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(4),
                Width = 120,
                Height = 25
            };

            var textBlock = new TextBlock
            {
                Text = "Exit Company",
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };

            border.Child = textBlock;
            exitButton.Content = border; exitButton.Click += ExitButton_Click;

            if (this.Content is Grid g)
            {
                g.Children.Add(exitButton);
            }
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine("[PAGE_REGISTRATION] Theme changed");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PAGE_REGISTRATION] OnThemeChanged failed: {ex}"); }
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

                var signUpButton = this.FindName("SignUpButton") as Button;
                if (signUpButton != null)
                {
                    signUpButton.Background = new SolidColorBrush(palette.Secondary);
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_REGISTRATION] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_REGISTRATION] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ExitCompany();
            this.NavigationService?.Navigate(new Pages.Page_install());
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterUsername, DefaultLanguage.ErrorTitle);
                FullNameTextBox.Focus();
                return;
            }

            if (fullName.Length < 3 || fullName.Length > 50)
            {
                MessageBox.ShowWarning(DefaultLanguage.UsernameLength, DefaultLanguage.ErrorTitle);
                FullNameTextBox.Focus();
                return;
            }

            if (!IsValidUsername(fullName))
            {
                MessageBox.ShowWarning(DefaultLanguage.UsernameInvalid, DefaultLanguage.ErrorTitle);
                FullNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterEmail, DefaultLanguage.ErrorTitle);
                EmailTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterValidEmail, DefaultLanguage.ErrorTitle);
                EmailTextBox.Focus();
                return;
            }

            if (TermsCheckBox.IsChecked != true)
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseAgreeTerms, DefaultLanguage.ErrorTitle);
                return;
            }

            SignUpButton.IsEnabled = false;
            SignUpButton.Content = DefaultLanguage.Sending;

            try
            {
                bool success = await App.AuthService.SendVerificationCodeAsync(email);

                if (success)
                {
                    bool rememberMe = RememberMeCheckBox?.IsChecked == true;
                    NavigationService.Navigate(new Page_verification(email, isRegistration: true, username: fullName, rememberMe: rememberMe));
                }
                else
                {
                    MessageBox.ShowError(DefaultLanguage.FailedSendVerification, DefaultLanguage.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.ShowError($"Error: {ex.Message}", DefaultLanguage.ErrorTitle);
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

            if (username.Length < 3)
            {
                SetUsernameValidationStyle(false, DefaultLanguage.UsernameLength);
                return;
            }

            if (username.Length > 50)
            {
                SetUsernameValidationStyle(false, DefaultLanguage.UsernameLength);
                return;
            }

            if (!IsValidUsername(username))
            {
                SetUsernameValidationStyle(false, DefaultLanguage.UsernameInvalid);
                return;
            }

            SetUsernameValidationStyle(true, "Valid username");
        }

        private bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            return UsernameRegex.IsMatch(username);
        }

        private void SetUsernameValidationStyle(bool isValid, string tooltip)
        {
            if (isValid)
            {
                FullNameTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(11, 69, 57));
                FullNameTextBox.ToolTip = tooltip;
            }
            else
            {
                FullNameTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                FullNameTextBox.ToolTip = tooltip;
            }
        }

        private void ResetUsernameValidationStyle()
        {
            FullNameTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            FullNameTextBox.ToolTip = null;
        }

        private void SignInText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Page_login());
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SignUpButton_Click(sender, e);
            }
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
                string exeFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? AppDomain.CurrentDomain.BaseDirectory) ?? string.Empty;
                string privacyDir = Path.Combine(exeFolder, "Privacy");

                string lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                string filename = lang == "uk" || lang == "ua" ? "terms_privacy_uk.txt" : "terms_privacy_en.txt";

                string filePath = Path.Combine(privacyDir, filename);

                string content;
                if (!File.Exists(filePath))
                {
                    var projectDir = Directory.GetParent(exeFolder)?.Parent?.Parent?.FullName ?? exeFolder;
                    filePath = Path.Combine(projectDir, "Privacy", filename);
                }

                if (File.Exists(filePath))
                {
                    content = await File.ReadAllTextAsync(filePath);
                }
                else
                {
                    content = DefaultLanguage.PoliciesContent;
                }

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