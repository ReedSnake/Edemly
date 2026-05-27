using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using MessageBox = uchat.Pages.MessageBox;
using uchat.Lang;
using uchat.Services;

namespace uchat
{
    public partial class Page_login : Page
    {
        public Page_login()
        {
            InitializeComponent();

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            var exitButton = new Button
            {
                Content = "Exit Company",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8),
                Width = 120, 
                Height = 25,
                Background = Brushes.White,
                Foreground = Brushes.Black,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 10, 16, 10) 
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2)); 

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            exitButton.Template = template; exitButton.Click += ExitButton_Click;

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
                System.Diagnostics.Debug.WriteLine("[PAGE_LOGIN] Theme changed");
            }
            catch { }
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

                if (LoginButton != null)
                {
                    LoginButton.Background = new SolidColorBrush(palette.Secondary);
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_LOGIN] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_LOGIN] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            App.ExitCompany();

            this.NavigationService?.Navigate(new Pages.Page_install());
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterEmail, DefaultLanguage.ErrorTitle);
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.ShowWarning(DefaultLanguage.PleaseEnterValidEmail, DefaultLanguage.ErrorTitle);
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = DefaultLanguage.Sending;

            try
            {
                bool success = await App.AuthService.SendVerificationCodeAsync(email);

                if (success)
                {
                    bool rememberMe = RememberMeCheckBox?.IsChecked == true;
                    NavigationService.Navigate(new Page_verification(email, isRegistration: false, rememberMe: rememberMe));
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
                LoginButton.IsEnabled = true;
                LoginButton.Content = DefaultLanguage.LoginButton;
            }
        }

        private void SignUpText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Page_registration());
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
        private void EmailTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }
    }
}
