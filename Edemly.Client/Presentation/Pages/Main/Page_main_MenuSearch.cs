#nullable disable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Edemly.Client.Presentation.Windows;
namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (IsSearchPlaceholderText(SearchTextBox.Text))
            {
                ApplyTextInputActiveStyle(SearchTextBox, string.Empty);
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
                    {
                        ApplyTextInputPlaceholderStyle(SearchTextBox, DefaultLanguage.SearchPlaceholder);
                    }
                    HideSearchResults();
                });
            });
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text;

            var resultsPanel = this.FindName("SearchResultsPanel") as StackPanel;
            if (resultsPanel != null && _chatController != null)
            {
                await _chatController.SearchAndCreateChatAsync(searchText, SearchTextBox, resultsPanel);
            }
        }

        private void HideSearchResults()
        {
            var searchBorder = this.FindName("SearchResultsBorder") as Border;
            if (searchBorder != null)
            {
                searchBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CloseSearchResults_Click(object sender, RoutedEventArgs e)
        {
            var searchBorder = this.FindName("SearchResultsBorder") as Border;
            if (searchBorder != null)
            {
                searchBorder.Visibility = Visibility.Collapsed;
            }

            var resultsPanel = this.FindName("SearchResultsPanel") as StackPanel;
            if (resultsPanel != null)
            {
                resultsPanel.Children.Clear();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        private void OpenMenu()
        {
            isMenuOpen = true;
            Overlay.Visibility = Visibility.Visible;

            DoubleAnimation animation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SideMenuTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void CloseMenu()
        {
            isMenuOpen = false;

            DoubleAnimation animation = new DoubleAnimation
            {
                To = -350,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SideMenuTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            Overlay.Visibility = Visibility.Collapsed;
        }

        private void Overlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CloseMenu();
        }

        private void MyPlannerButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CalendarPage());
        }

        private void ContactsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(DefaultLanguage.Contacts + " clicked!");
            CloseMenu();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Page_settings());
            CloseMenu();
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(DefaultLanguage.Profile + " clicked!");
            CloseMenu();
        }

        private void AboutApp_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow?.MainFrame != null)
            {
                mainWindow.MainFrame.Navigate(new Page_aboutapp());
            }
            CloseMenu();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.ShowQuestion(DefaultLanguage.LogoutConfirm, DefaultLanguage.Logout);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    DisposeCancellationTokenSourceSafely();

                    if (App.HubService != null)
                    {
                        await App.HubService.DisconnectAsync();
                    }

                    if (App.AuthService != null)
                    {
                        await App.AuthService.LogoutAsync();
                    }

                    App.ClearCurrentUser();

                    NavigationService.Navigate(new LoginPage());
                }
                catch (Exception ex)
                {
                    MessageBox.ShowError(string.Format(DefaultLanguage.LogoutError, ex.Message), DefaultLanguage.ErrorTitle);
                }
            }

            CloseMenu();
        }

        private void PremiumButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Page_premium());
            CloseMenu();
        }
    }
}
