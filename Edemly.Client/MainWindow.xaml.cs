using System;
using System.Windows;
using Edemly.Client.Controls;
using Edemly.Client.Services;
using Edemly.Client.DTOs;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;

namespace Edemly.Client
{
    public partial class MainWindow : Window
    {
        private bool _isAutoLoginCompleted = false;
        private readonly string _baseTitle;

        public MainWindow()
        {
            InitializeComponent();

            // save base title from XAML
            _baseTitle = this.Title;

            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            // ✅ ВИПРАВЛЕНО: Використовуємо правильну назвту з XAML
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

                        // If concrete HubService exposes IsReconnecting, use it
                        bool isReconnecting = false;
                        try
                        {
                            var concrete = App.HubService as Edemly.Client.Services.HubService;
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

            // Підписуємося на події HubService
            if (App.HubService != null)
            {
                System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] Subscribing to App.HubService.ConnectionStateChanged in constructor");
                App.HubService.ConnectionStateChanged += OnConnectionStateChanged;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MAIN_WINDOW] App.HubService is null in constructor, subscribing to App.Dispatcher.ShutdownStarted to try again");
            }

            // Додатково: гарантуємо підписку навіть якщо HubService створюється після MainWindow
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
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

            // Підписуємося на подію навігації, щоб оновлювати заголовок при переході на Page_main
            MainFrame.Navigated += MainFrame_Navigated;

            // ✅ ВИПРАВЛЕНО: Підписуємося на подію Closing
            this.Closing += MainWindow_Closing;

            // Intercept window messages after source initialized
            this.SourceInitialized += MainWindow_SourceInitialized;

            // If installation not completed, show install page first
            try
            {
                if (!ConfigService.Instance.IsInstalled)
                {
                    // Navigate to install page and skip auto-login
                    MainFrame.Navigate(new Pages.Page_install());
                    _isAutoLoginCompleted = true; // avoid waiting in Closing
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to check installation config: {ex.Message}");
            }

            // ✅ ДОДАНО: Спробувати автоматичний вхід
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

        // called when navigation completes (used to refresh title after manual login/navigation)
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

        // Intercept WM_NCHITTEST to disable border resizing but keep caption and maximize working
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
                // Ask default window proc for the real hit-test result
                IntPtr defaultResult = DefWindowProc(hwnd, msg, wParam, lParam);
                int code = defaultResult.ToInt32();

                // If it's a sizing border, map it to caption so the window won't be resizable by dragging borders
                if (code == HTLEFT || code == HTRIGHT || code == HTTOP || code == HTTOPLEFT || code == HTTOPRIGHT || code == HTBOTTOM || code == HTBOTTOMLEFT || code == HTBOTTOMRIGHT)
                {
                    handled = true;
                    return new IntPtr(HTCAPTION);
                }

                // Otherwise let default behavior happen
                handled = false;
                return defaultResult;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ✅ ДОДАНО: Обробник закриття вікна
        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Window closing...");

                // Якщо автоматичний вхід ще не завершився, чекаємо трохи
                if (!_isAutoLoginCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Auto-login not completed, waiting...");

                    // Скасовуємо закриття та показуємо діалог
                    e.Cancel = true;

                    // Чекаємо максимум 2 секунди
                    int waited = 0;
                    while (!_isAutoLoginCompleted && waited < 2000)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        waited += 100;
                    }

                    // Закриваємо вікно програмно
                    this.Closing -= MainWindow_Closing; // Видаляємо обробник щоб уникнути рекурсії
                    this.Close();
                    return;
                }

                // Відключаємося від HubService
                if (App.HubService != null && App.HubService.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Disconnecting from hub...");
                    await App.HubService.DisconnectAsync();
                }

                // Очищаємо глобальний ChatManager
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

        // ✅ ВИПРАВЛЕНО: Додано флаг завершення
        private async void TryAutoLoginAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Attempting auto-login...");

                // Завантажуємо збережені дані автентифікації
                var authData = App.AuthService.LoadAuthData();

                if (authData != null && !string.IsNullOrEmpty(authData.SessionToken))
                {
                    System.Diagnostics.Debug.WriteLine($"[MAIN WINDOW] Found saved auth data for user: {authData.Username}");

                    // Пробуємо увійти через session token
                    var sessionResponse = await App.AuthService.SessionLoginAsync(authData.SessionToken);

                    if (sessionResponse != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Session login successful!");

                        // Зберігаємо дані користувача
                        App.SetCurrentUser(
                            sessionResponse.UserId,
                            sessionResponse.Email,
                            sessionResponse.Username,
                            null,
                            sessionResponse.Token
                        );

                        // Встановлюємо токен для API
                        App.ApiService.SetAuthToken(sessionResponse.Token);
                        await App.RefreshCurrentUserProfileAsync();

                        // Підключаємося до хабу
                        bool connected = await App.HubService.ConnectAsync(sessionResponse.Token);

                        if (connected)
                        {
                            System.Diagnostics.Debug.WriteLine("[MAIN WINDOW] Hub connection successful");
                        }

                        // Оновлюємо заголовок вікна з підпискою
                        await RefreshWindowTitleAsync();

                        // Переходимо на головну сторінку
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

                // Якщо автоматичний вхід не вдався, показуємо сторінку логіну
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
                        // Check whether hub is currently in reconnecting state
                        bool isReconnecting = false;
                        try
                        {
                            var concrete = App.HubService as Edemly.Client.Services.HubService;
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
            // Відписуємося від події
            if (App.HubService != null)
            {
                App.HubService.ConnectionStateChanged -= OnConnectionStateChanged;
            }

            MainFrame.Navigated -= MainFrame_Navigated; // cleanup

            base.OnClosed(e);
        }

        /// <summary>
        /// Оновити заголовок вікна, додаючи підписку (Free/Premium/Vip)
        /// - Нічого не показуємо, якщо користувач не авторизований.
        /// - За замовчуванням нічого не показуємо для company install.
        ///   Якщо потрібно, можна розкоментувати відповідний рядок щоб показувати "Premium" для company.
        /// </summary>
        private async Task RefreshWindowTitleAsync()
        {
            try
            {
                // default
                this.Title = _baseTitle;

                // якщо користувач не залогінений — нічого не показуємо
                if (!App.CurrentUserId.HasValue)
                    return;

                // якщо інсталяція через компанію — за замовчуванням нічого не показуємо
                var cfg = ConfigService.Instance;
                bool isCompany = cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company);
                if (isCompany)
                {
                    // Якщо хочете завжди показувати "Premium" для company, замініть return на рядок нижче:
                    // this.Title = $"{_baseTitle} — Premium";
                    return;
                }

                // Запитуємо інформацію про користувача з API (повинно повернути SubscriptionStatus)
                UserInfoDto? info = null;
                try
                {
                    info = await App.ApiService.GetUserInfo();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get user info for title: {ex.Message}");
                }

                // Use null-conditional to safely read SubscriptionStatus from a nullable DTO
                var sub = (info?.SubscriptionStatus ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(sub)) return;

                // Normalize (server returns e.g. "Free", "Premium", "Vip")
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