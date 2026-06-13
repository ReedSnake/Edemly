using Edemly.Client.Presentation.Controls;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Pages.Auth;
using Edemly.Client.Presentation.Pages.Main;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Edemly.Client.Infrastructure.Storage;
using Edemly.Contracts.Users;

namespace Edemly.Client.Presentation.Windows
{
    public partial class MainWindow : ThemedWindow
    {
        private bool _isAutoLoginCompleted = false;
        private readonly string _baseTitle;

        public MainWindow()
        {
            InitializeComponent();

            _baseTitle = this.Title;

            App.StatusBar = ConnectionStatusBar;

            MainFrame.Navigated += MainFrame_Navigated;

            this.Closing += MainWindow_Closing;

            this.SourceInitialized += MainWindow_SourceInitialized;

            try
            {
                if (!ConfigService.Instance.IsInstalled)
                {
                    MainFrame.Navigate(new InstallPage());
                    _isAutoLoginCompleted = true; // avoid waiting in Closing
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check installation config: {ex.Message}");
            }

            TryAutoLoginAsync();
        }

        private async void MainFrame_Navigated(object? sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            try
            {
                await RefreshWindowTitleAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] MainFrame_Navigated failed: {ex}");
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;
            const int HTCAPTION = 2;

            if (msg == WM_NCHITTEST)
            {
                IntPtr defaultResult = DefWindowProc(hwnd, msg, wParam, lParam);
                int code = defaultResult.ToInt32();

                if (code == HTLEFT || code == HTRIGHT || code == HTTOP || code == HTTOPLEFT || code == HTTOPRIGHT || code == HTBOTTOM || code == HTBOTTOMLEFT || code == HTBOTTOMRIGHT)
                {
                    handled = true;
                    return new IntPtr(HTCAPTION);
                }

                handled = false;
                return defaultResult;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Window closing...");

                if (!_isAutoLoginCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Auto-login not completed, waiting...");

                    e.Cancel = true;

                    int waited = 0;
                    while (!_isAutoLoginCompleted && waited < 2000)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        waited += 100;
                    }

                    this.Closing -= MainWindow_Closing; // Видаляємо обробник щоб уникнути рекурсії
                    this.Close();
                    return;
                }

                if (App.HubService != null && App.HubService.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Disconnecting from hub...");
                    await App.HubService.DisconnectAsync();
                }

                if (App.GlobalChatController != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Disposing global chat controller...");
                    App.GlobalChatController.Dispose();
                    App.GlobalChatController = null;
                }

                System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Window closed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN WINDOW] Error during window closing: {ex.Message}");
            }
        }

        private async void TryAutoLoginAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Attempting auto-login...");

                var authData = App.AuthService.LoadAuthData();

                if (authData != null && !string.IsNullOrEmpty(authData.SessionToken))
                {
                    System.Diagnostics.Debug.WriteLine($"[MAIN WINDOW] Found saved auth data for user: {authData.Username}");

                    var sessionResponse = await App.AuthService.SessionLoginAsync(authData.SessionToken);

                    if (sessionResponse != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Session login successful!");

                        App.SetCurrentUser(
                            sessionResponse.UserId,
                            sessionResponse.Email,
                            sessionResponse.Username,
                            null,
                            sessionResponse.Token
                        );

                        await App.RefreshCurrentUserProfileAsync();

                        App.ConnectRealtimeInBackground(sessionResponse.Token);

                        await RefreshWindowTitleAsync();

                        MainFrame.Navigate(new MainPage());
                        _isAutoLoginCompleted = true;
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Session expired, clearing auth data");
                        App.AuthService.ClearAuthData();
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] No saved auth data found");
                }

                MainFrame.Navigate(new LoginPage());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN WINDOW] Auto-login error: {ex.Message}");
                MainFrame.Navigate(new LoginPage());
            }
            finally
            {
                _isAutoLoginCompleted = true;
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            App.StatusBar = null;
            MainFrame.Navigated -= MainFrame_Navigated;

            base.OnClosed(e);
        }

        private async Task RefreshWindowTitleAsync()
        {
            try
            {
                this.Title = _baseTitle;

                if (!App.CurrentUserId.HasValue)
                    return;

                var cfg = ConfigService.Instance;
                bool isCompany = cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company);
                if (isCompany)
                {
                    return;
                }

                UserInfoDto? info = null;
                try
                {
                    info = await App.ApiClients.Users.GetUserInfoAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get user info for title: {ex.Message}");
                }

                var sub = (info?.SubscriptionStatus ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(sub)) return;

                string display = sub switch
                {
                    var s when s.Equals("free", StringComparison.OrdinalIgnoreCase) => "Free",
                    var s when s.Equals("premium", StringComparison.OrdinalIgnoreCase) => "Premium",
                    var s when s.Equals("vip", StringComparison.OrdinalIgnoreCase) => "Vip",
                    _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sub.ToLowerInvariant())
                };

                this.Title = $"{_baseTitle} [{display}]";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshWindowTitleAsync error: {ex.Message}");
            }
        }
    }
}
