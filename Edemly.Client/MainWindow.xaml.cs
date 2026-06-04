using Edemly.Client.Controls;
using Edemly.Client.Realtime;
using Edemly.Client.Services;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Edemly.Client
{
    public partial class MainWindow : Window
    {
        private bool _isAutoLoginCompleted = false;
        private readonly string _baseTitle;

        public MainWindow()
        {
            InitializeComponent();

            _baseTitle = this.Title;

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            App.StatusBar = ConnectionStatusBar;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (App.HubService != null)
                    {
                        var hasAuth = !string.IsNullOrWhiteSpace(App.AuthToken);
                        if (!hasAuth)
                        {
                            ConnectionStatusBar.Hide();
                            return;
                        }

                        bool isReconnecting = false;
                        try
                        {
                            var concrete = (((App.HubService as HubService)));
                            if (concrete != null) isReconnecting = concrete.IsReconnecting;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] Failed to read IsReconnecting: {ex}");
                        }

                        if (App.HubService.IsConnected)
                        {
                            ConnectionStatusBar.ShowConnected();
                        }
                        else if (isReconnecting)
                        {
                            ConnectionStatusBar.ShowReconnecting();
                        }
                        else
                        {
                            ConnectionStatusBar.Hide();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] Initial status bar update failed: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            if (App.HubService != null)
            {
                System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] Subscribing to App.HubService.ConnectionStateChanged in constructor");
                App.HubService.ConnectionStateChanged += OnConnectionStateChanged;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] App.HubService is null in constructor, subscribing to App.Dispatcher.ShutdownStarted to try again");
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (App.HubService != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] (Dispatcher) Subscribing to App.HubService.ConnectionStateChanged");
                    App.HubService.ConnectionStateChanged -= OnConnectionStateChanged; // avoid double
                    App.HubService.ConnectionStateChanged += OnConnectionStateChanged;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] (Dispatcher) App.HubService is still null");
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            MainFrame.Navigated += MainFrame_Navigated;

            this.Closing += MainWindow_Closing;

            this.SourceInitialized += MainWindow_SourceInitialized;

            try
            {
                if (!ConfigService.Instance.IsInstalled)
                {
                    MainFrame.Navigate(new Pages.Page_install());
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

        private void OnThemeChanged()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] Theme changed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] OnThemeChanged failed: {ex}");
            }
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

                if (App.GlobalChatManager != null)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Disposing ChatManager...");
                    App.GlobalChatManager.Dispose();
                    App.GlobalChatManager = null;
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

                        App.ApiService.SetAuthToken(sessionResponse.Token);
                        await App.RefreshCurrentUserProfileAsync();

                        bool connected = await App.HubService.ConnectAsync(sessionResponse.Token);

                        if (connected)
                        {
                            System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Hub connection successful");
                        }

                        await RefreshWindowTitleAsync();

                        MainFrame.Navigate(new Page_main());
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

                MainFrame.Navigate(new Page_login());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN WINDOW] Auto-login error: {ex.Message}");
                MainFrame.Navigate(new Page_login());
            }
            finally
            {
                _isAutoLoginCompleted = true;
            }
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] OnConnectionStateChanged: isConnected={isConnected}, Window.IsLoaded={this.IsLoaded}");

                var dispatcher = this.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] Dispatcher not available or shutting down, skipping status bar update");
                    return;
                }

                dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        bool isReconnecting = false;
                        try
                        {
                            var concrete = (((App.HubService as HubService)));
                            if (concrete != null) isReconnecting = concrete.IsReconnecting;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] OnConnectionStateChanged inner failed: {ex}");
                        }

                        if (isConnected)
                        {
                            System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] ShowConnected()");
                            ConnectionStatusBar.ShowConnected();
                        }
                        else if (isReconnecting)
                        {
                            System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] ShowReconnecting()");
                            ConnectionStatusBar.ShowReconnecting();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] ShowDisconnected()");
                            ConnectionStatusBar.ShowDisconnected();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] OnConnectionStateChanged UI action failed: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN_WINDOW] OnConnectionStateChanged exception: {ex.Message}");
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (App.HubService != null)
            {
                App.HubService.ConnectionStateChanged -= OnConnectionStateChanged;
            }

            MainFrame.Navigated -= MainFrame_Navigated; // cleanup

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
                    info = await App.ApiService.GetUserInfoAsync();
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