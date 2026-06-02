using System;
using System.Linq;
using System.Windows;
using Edemly.Client.Services;
using Edemly.Client.Helpers;
using CommunityToolkit.WinUI.Notifications;
using Edemly.Client.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Edemly.Client.Services.Api;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace Edemly.Client
{
    public partial class App : Application
    {
        private const string AppId = "Edemly.MainApp";
        // Глобальні сервіси
        public static IApiService ApiService { get; private set; } = null!;
        public static IAuthService AuthService { get; private set; } = null!;
        public static IHubService HubService { get; private set; } = null!;
        public static NotesService? NotesService { get; private set; }

        // Глобальні кеші
        public static ChatCache GlobalChatCache { get; private set; } = new ChatCache();
        public static ProfilePictureCache GlobalProfilePictureCache { get; private set; } = null!;
        public static FileCache GlobalFileCache { get; private set; } = null!;

        // Глобальний ChatManager
        public static ChatManager? GlobalChatManager { get; set; }

        // ConnectionStatusBar reference
        public static Edemly.Client.Controls.ConnectionStatusBar? StatusBar { get; set; }

        // Дані користувача
        public static int? CurrentUserId { get; set; }
        public static string? CurrentUserEmail { get; set; }
        public static string? CurrentUserName { get; set; }
        public static string? CurrentUserPhotoUrl { get; set; }
        public static string? AuthToken { get; set; }

        // Кеш для швидкого доступу до чатів (для Toast notifications)
        private static readonly Dictionary<int, (DTOs.ChatDto chat, List<DTOs.ChatMemberDto> members)> _chatDataCache = new();
        private static readonly object _chatCacheLock = new object();

        // Store base server url (without tenant) to allow switching company at runtime
        public static string BaseServerUrlNoCompany { get; private set; } = string.Empty;

        public static string? CurrentUsername
        {
            get => CurrentUserName;
            set => CurrentUserName = value;
        }

        private ApiService _apiService;
        private ProfilePictureCache _pfpCache;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Ensure language is loaded from config before MainWindow or pages are created.
            if (e.Args.Length < 1 || string.IsNullOrWhiteSpace(e.Args[0]))
            {
                MessageBox.Show(
                    "Server URL missing. Example:\nEdemly.exe https://your-server.com",
                    "Config Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            try
            {
                var savedLang = ConfigService.Instance?.Language;
                if (string.IsNullOrWhiteSpace(savedLang))
                    savedLang = "en";

                // Load translation files and persist choice in config
                try
                {
                    LanguageService.Instance.LoadLanguage(savedLang);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] LoadLanguage failed: {ex}");
                }

                // Load and apply theme
                try
                {
                    ThemeService.Instance.LoadAndApplyTheme();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] LoadAndApplyTheme failed: {ex}");
                }

                // Set thread cultures to match chosen language where possible
                try
                {
                    CultureInfo cultureInfo;
                    if (string.Equals(savedLang, "uk", StringComparison.OrdinalIgnoreCase))
                        cultureInfo = new CultureInfo("uk-UA");
                    else if (string.Equals(savedLang, "en", StringComparison.OrdinalIgnoreCase))
                        cultureInfo = new CultureInfo("en-US");
                    else
                        cultureInfo = new CultureInfo(savedLang);

                    CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                    CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] Set culture failed: {ex}");
                }

                System.Diagnostics.Debug.WriteLine($"[APP] Language set to: {LanguageService.Instance.CurrentLanguage}");

                // Apply saved wallpaper (if any) so it's available immediately to pages using DynamicResource BackgroundImage
                try
                {
                    var bgPath = ConfigService.Instance?.BackgroundImagePath;
                    if (!string.IsNullOrWhiteSpace(bgPath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(bgPath, UriKind.RelativeOrAbsolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();

                            Application.Current.Resources["BackgroundImage"] = bmp;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[APP] Failed to load BackgroundImage '{bgPath}': {ex.Message}");
                            Application.Current.Resources["BackgroundImage"] = null;
                        }
                    }
                    else
                    {
                        // ensure key exists (App.xaml already sets x:Null) - keep as is
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] Error while applying wallpaper: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Startup error: {ex}");
            }

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                Current.Dispatcher.Invoke(async () =>
                {
                    try
                    {
                        var args = ToastArguments.Parse(toastArgs.Argument);

                        if (args.TryGetValue("action", out string action) && action == "viewChat")
                        {
                            if (args.TryGetValue("chatId", out string chatIdStr) && int.TryParse(chatIdStr, out int chatId))
                            {
                                OpenChatWindow(chatId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error in Toast: {ex.Message}");
                    }
                });
            };

            base.OnStartup(e);

            // Global unhandled exception handlers to capture crashes and log them
            this.DispatcherUnhandledException += (sender, args) =>
            {
                try
                {
                    Debug.WriteLine($"[APP][UNHANDLED] DispatcherUnhandledException: {args.Exception}");
                    MessageBox.Show($"Unhandled UI exception: {args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex) { Debug.WriteLine($"[APP][UNHANDLED] Failed to show DispatcherUnhandledException: {ex}"); }
                finally { args.Handled = true; }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                try
                {
                    var ex = args.ExceptionObject as Exception;
                    Debug.WriteLine($"[APP][UNHANDLED] DomainUnhandledException: {ex}");
                    // Show message on UI thread if possible
                    Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        try { MessageBox.Show($"Fatal error: {ex?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch (Exception ex2) { Debug.WriteLine($"[APP][UNHANDLED] Failed to show fatal error dialog: {ex2}"); }
                    }));
                }
                catch (Exception ex) { Debug.WriteLine($"[APP][UNHANDLED] Domain handler failed: {ex}"); }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                try
                {
                    Debug.WriteLine($"[APP][UNOBSERVED] TaskScheduler.UnobservedTaskException: {args.Exception}");
                    args.SetObserved();
                }
                catch (Exception ex) { Debug.WriteLine($"[APP][UNOBSERVED] Failed to handle unobserved task exception: {ex}"); }
            };

            // Read server base URL from first command line arg only. Make it required.
            string? serverUrl = null;
            string? tenantArg = null;

            try
            {
                var argsList = Environment.GetCommandLineArgs();

                // Expect the first argument after executable path to be the server URL
                if (argsList.Length > 1)
                {
                    // find first non-switch token to be server URL
                    for (int i = 1; i < argsList.Length; i++)
                    {
                        var raw = argsList[i].Trim();
                        if (string.IsNullOrEmpty(raw)) continue;

                        // treat switches starting with '-' or '/' as flags
                        if (raw.StartsWith("--") || raw.StartsWith("-"))
                        {
                            // check tenant/company flag
                            if (raw.StartsWith("--tenant", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("--company", StringComparison.OrdinalIgnoreCase))
                            {
                                // If written as --tenant=value
                                var parts = raw.Split(new[] { '=' }, 2);
                                if (parts.Length == 2)
                                {
                                    tenantArg = parts[1].Trim().Trim('"');
                                }
                                else
                                {
                                    // next arg is value
                                    if (i + 1 < argsList.Length)
                                    {
                                        tenantArg = argsList[i + 1].Trim().Trim('"');
                                        i++; // consume
                                    }
                                }

                                continue;
                            }

                            // unknown flag - skip
                            continue;
                        }

                        // otherwise treat as server URL if not set yet
                        if (serverUrl == null)
                        {
                            var candidate = raw;

                            // If user passed without scheme (like "edemly.me" or "www.edemly.me"), prepend https://
                            if (!candidate.Contains("://"))
                            {
                                candidate = "https://" + candidate;
                            }

                            if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
                            {
                                serverUrl = parsed.ToString().TrimEnd('/');
                            }

                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Command line parsing error: {ex}");
            }

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                System.Windows.MessageBox.Show(
                    "Server URL is missing.\n\nPlease provide it as the first command-line argument.\nExample:\n    Edemly.exe https://your-server.com",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[APP] Using server URL: {serverUrl}");

            // After parsing serverUrl
            BaseServerUrlNoCompany = serverUrl!; // save base

            // If tenantArg provided via --tenant or --company, store it into config so subsequent service creation uses it
            try
            {
                if (!string.IsNullOrWhiteSpace(tenantArg))
                {
                    var cfg = ConfigService.Instance;
                    if (!string.IsNullOrWhiteSpace(tenantArg) && !string.Equals(tenantArg, "Personal", StringComparison.OrdinalIgnoreCase))
                    {
                        cfg.Company = tenantArg.Trim();
                        cfg.IsInstalled = true; // treat as pre-configured
                        cfg.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] ConfigService update error: {ex}");
            }

            // Determine actual API base depending on installation/company
            var cfg2 = ConfigService.Instance;
            string apiBase = BaseServerUrlNoCompany;
            if (cfg2.IsInstalled && !string.IsNullOrWhiteSpace(cfg2.Company) && !string.Equals(cfg2.Company, "Personal", StringComparison.OrdinalIgnoreCase))
            {
                apiBase = BaseServerUrlNoCompany.TrimEnd('/') + "/" + cfg2.Company.Trim().Trim('/');
            }

            ApiService = new ApiService(apiBase);
            AuthService = new AuthService(apiBase);
            HubService = new HubService(apiBase);

            // Subscribe to incoming call events from concrete HubService and forward to UI
            try
            {
                var concreteHub = HubService as Edemly.Client.Services.HubService;
                if (concreteHub != null)
                {
                    concreteHub.IncomingCallReceived += GlobalIncomingCallHandler;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to subscribe incoming call handler: {ex}");
            }

            // Diagnostic: log event subscription
            try
            {
                System.Diagnostics.Debug.WriteLine("[APP] Subscribing to HubService.ConnectionStateChanged");
                HubService.ConnectionStateChanged -= OnConnectionStateChanged; // avoid double
                HubService.ConnectionStateChanged += OnConnectionStateChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] Failed to subscribe to HubService.ConnectionStateChanged: {ex.Message}");
            }

            // Use cache scope based on selected company (empty = personal)
            var cacheScope = string.IsNullOrWhiteSpace(cfg2.Company) ? "personal" : cfg2.Company.Trim();
            // Provide a live token provider that reads current App.AuthToken (or SecureStorage)
            GlobalProfilePictureCache = new ProfilePictureCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);
            GlobalFileCache = new FileCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);

            System.Diagnostics.Debug.WriteLine("[APP] Starting NotesService initialization...");

            try
            {
                NotesService = new NotesService(ApiService);
                System.Diagnostics.Debug.WriteLine("[APP] NotesService instance created");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] Failed to initialize NotesService: {ex.Message}");
            }

            HubService.ConnectionStateChanged += OnConnectionStateChanged;

            // Create and show main window explicitly (only after successful initialization)
            try
            {
                var mainWindow = new MainWindow();
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to create MainWindow: {ex}");
                // If we fail to create the main window, ensure app exits
                Shutdown();
            }
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            Current.Dispatcher.Invoke(() =>
            {
                if (StatusBar != null)
                {
                    if (string.IsNullOrWhiteSpace(AuthToken))
                    {
                        StatusBar.Hide();
                        return;
                    }

                    if (isConnected)
                    {
                        StatusBar.ShowConnected();
                    }
                    else
                    {
                        // Determine if the hub is actively reconnecting
                        bool isReconnecting = false;
                        try
                        {
                            var concrete = HubService as Edemly.Client.Services.HubService;
                            if (concrete != null) isReconnecting = concrete.IsReconnecting;
                        }
                        catch (Exception ex) { Debug.WriteLine($"[APP] Failed to read hub reconnecting state: {ex.Message}"); }

                        if (isReconnecting)
                        {
                            StatusBar.ShowReconnecting();
                        }
                        else
                        {
                            StatusBar.Hide();
                        }
                    }
                }
            });
        }

        public static void SetCurrentUser(int userId, string email, string username, string? photoUrl = null, string? token = null)
        {
            CurrentUserId = userId;
            CurrentUserEmail = email;
            CurrentUserName = username;
            CurrentUserPhotoUrl = photoUrl;

            if (!string.IsNullOrEmpty(token))
            {
                AuthToken = token;
                try { ApiService?.SetAuthToken(token); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[APP] ApiService.SetAuthToken failed: {ex}"); }

                try { GlobalProfilePictureCache?.SetAuthToken(token); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[APP] GlobalProfilePictureCache.SetAuthToken failed: {ex}"); }
            }

            System.Diagnostics.Debug.WriteLine($"[APP] User set: ID={userId}, Email={email}, Name={username}");
        }

        public static async Task RefreshCurrentUserProfileAsync()
        {
            try
            {
                if (ApiService == null || !CurrentUserId.HasValue)
                {
                    return;
                }

                var userInfo = await ApiService.GetUserInfo();
                if (userInfo == null || userInfo.Id <= 0)
                {
                    return;
                }

                CurrentUserPhotoUrl = userInfo.PfpUrl;
                Pages.MyInfo.UserName = userInfo.Username ?? string.Empty;
                Pages.MyInfo.Email = userInfo.Email ?? string.Empty;
                Pages.MyInfo.PhoneNumber = userInfo.PhoneNumber ?? string.Empty;
                Pages.MyInfo.PfpUrl = userInfo.PfpUrl ?? string.Empty;
                Pages.MyInfo.Description = userInfo.Description ?? string.Empty;
                Pages.MyInfo.FirstName = userInfo.FirstName ?? string.Empty;
                Pages.MyInfo.LastName = userInfo.LastName ?? string.Empty;
                Pages.MyInfo.Name = $"{Pages.MyInfo.FirstName} {Pages.MyInfo.LastName}".Trim();
                if (string.IsNullOrEmpty(Pages.MyInfo.Name))
                {
                    Pages.MyInfo.Name = Pages.MyInfo.UserName;
                }

                if (!string.IsNullOrWhiteSpace(userInfo.PfpUrl))
                {
                    try { await GlobalProfilePictureCache.GetOrDownloadAsync(userInfo.PfpUrl); }
                    catch (Exception ex) { Debug.WriteLine($"[APP] Preload profile picture failed: {ex}"); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] RefreshCurrentUserProfileAsync failed: {ex.Message}");
            }
        }

        private static async Task EnsureHubConnectedAndRestoreCallsAsync()
        {
            try
            {
                // wait briefly for HubService to become available and connected
                int waited = 0;
                while (HubService == null && waited < 5000)
                {
                    await Task.Delay(100);
                    waited += 100;
                }

                if (HubService == null) return;

                // If not connected, try to connect using current token
                try
                {
                    if (!HubService.IsConnected && !string.IsNullOrEmpty(AuthToken))
                    {
                        try
                        {
                            await HubService.ConnectAsync(AuthToken);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[APP] EnsureHubConnected: failed to connect hub: {ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] EnsureHubConnectedAndRestoreCallsAsync failed: {ex}");
                }

                // Query active calls from API and open CallWindow if needed
                try
                {
                    var calls = await ApiService.GetActiveCallsAsync();
                    if (calls != null && calls.Count > 0)
                    {
                        // open the CallWindow on the UI thread
                        Current.Dispatcher.Invoke(() =>
                        {
                            // If a CallWindow already exists, activate it instead of creating a duplicate
                            var existing = System.Windows.Application.Current.Windows.OfType<Pages.CallWindow>().FirstOrDefault();
                            if (existing != null)
                            {
                                try
                                {
                                    if (!existing.IsVisible)
                                    {
                                        existing.Owner = Current.MainWindow;
                                        existing.Show();
                                    }
                                    else
                                    {
                                        existing.Activate();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[APP] Activate existing CallWindow failed: {ex}");
                                }
                            }
                            else
                            {
                                var win = new Pages.CallWindow();
                                win.Owner = Current.MainWindow;
                                win.Show();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to query active calls after login: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] EnsureHubConnectedAndRestoreCallsAsync outer failed: {ex}");
            }
        }

        public static void ClearCurrentUser()
        {
            CurrentUserId = null;
            CurrentUserEmail = null;
            CurrentUserName = null;
            CurrentUserPhotoUrl = null;
            AuthToken = null;
            GlobalProfilePictureCache?.SetAuthToken(null); // clear auth on cache

            GlobalChatManager?.Dispose();
            GlobalChatManager = null;

            Current.Dispatcher.Invoke(() =>
            {
                StatusBar?.Hide();
            });

            GlobalChatCache.ClearAll();

            // Clear and remove profile/file caches when user logs out
            try { GlobalProfilePictureCache?.ClearAll(); } catch (Exception ex) { Debug.WriteLine($"[APP] ClearAll ProfilePictureCache failed: {ex}"); }
            try { GlobalFileCache?.ClearAll(); } catch (Exception ex) { Debug.WriteLine($"[APP] ClearAll FileCache failed: {ex}"); }

            NotesService?.ClearCache();

            lock (_chatCacheLock)
            {
                _chatDataCache.Clear();
            }

            System.Diagnostics.Debug.WriteLine("[APP] All user data and caches cleared");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ToastNotificationManagerCompat.History.Clear();

            ToastNotificationManagerCompat.Uninstall();

            (HubService as IDisposable)?.Dispose();
            (ApiService as IDisposable)?.Dispose();
            GlobalChatCache?.Dispose();
            GlobalProfilePictureCache?.Dispose();
            GlobalFileCache?.Dispose();

            base.OnExit(e);
        }

        private async void OpenChatWindow(int chatId)
        {
            try
            {
                var mainWindow = Current.MainWindow as MainWindow;

                if (mainWindow == null)
                {
                    mainWindow = new MainWindow();
                    Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }

                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                mainWindow.Activate();
                mainWindow.Focus();

                int waitTime = 0;
                while (GlobalChatManager == null && waitTime < 2000)
                {
                    await Task.Delay(100);
                    waitTime += 100;
                }

                if (GlobalChatManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("ChatManager не ініціалізовано після очікування");
                    return;
                }

                DTOs.ChatDto? chat;
                List<DTOs.ChatMemberDto>? members = null;

                lock (_chatCacheLock)
                {
                    if (_chatDataCache.TryGetValue(chatId, out var cachedData))
                    {
                        chat = cachedData.chat;
                        members = cachedData.members;
                    }
                    else
                    {
                        chat = null;
                    }
                }

                if (chat == null)
                {
                    var chatsTask = ApiService.GetMyChatsAsync();
                    var membersTask = ApiService.GetChatMembersAsync(chatId);

                    await Task.WhenAll(chatsTask, membersTask);

                    var chats = await chatsTask;
                    members = await membersTask;
                    chat = chats.FirstOrDefault(c => c.Id == chatId);

                    if (chat == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Чат {chatId} не знайдено");
                        return;
                    }

                    lock (_chatCacheLock)
                    {
                        _chatDataCache[chatId] = (chat, members);
                    }
                }

                Models.Contact? contact = null;

                if (chat.Type == 0) // Приватний чат
                {
                    var otherMember = members?.FirstOrDefault(m => m.UserId != CurrentUserId);

                    if (otherMember != null)
                    {
                        var user = await ApiService.GetUserByIdAsync(otherMember.UserId);
                        if (user != null)
                        {
                            var photoPath = string.IsNullOrEmpty(user.PfpUrl)
                                ? "pack://application:,,,/Assets/avatar.png"
                                : user.PfpUrl;

                            contact = new Models.Contact(
                                user.Id,
                                user.Username,
                                user.Email ?? string.Empty,
                                user.PhoneNumber ?? string.Empty,
                                photoPath
                            );
                        }
                    }
                }
                else // Груповий чат
                {
                    var photoPath = string.IsNullOrEmpty(chat.IconUrl)
                        ? "pack://application:,,,/Assets/avatar.png"
                        : chat.IconUrl;

                    string groupName = string.IsNullOrWhiteSpace(chat.Name)
                        ? $"Group {chat.Id}"
                        : chat.Name;

                    contact = new Models.Contact(
                        chat.Id,
                        groupName,
                        "",
                        "",
                        photoPath
                    );
                }

                if (contact != null)
                {
                    await SwitchToChatDirectAsync(contact, chatId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка відкриття чату: {ex.Message}");
            }
        }

        private async Task SwitchToChatDirectAsync(Models.Contact contact, int chatId)
        {
            if (GlobalChatManager == null) return;

            try
            {
                await GlobalChatManager.SwitchToChatPublicAsync(contact, chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка переключення чату: {ex.Message}");
            }
        }

        public static void SetCompanyAndApply(string? company, bool markInstalled)
        {
            try
            {
                // normalize company -> empty means personal
                var cfg = ConfigService.Instance;
                if (string.IsNullOrWhiteSpace(company) || string.Equals(company, "Personal", StringComparison.OrdinalIgnoreCase))
                {
                    cfg.Company = string.Empty;
                }
                else
                {
                    cfg.Company = company.Trim();
                }

                cfg.IsInstalled = markInstalled;
                cfg.Save();

                // Dispose old services
                try { (HubService as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose HubService failed: {ex}"); }
                try { (ApiService as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose ApiService failed: {ex}"); }

                // Dispose and clear old caches
                try { GlobalProfilePictureCache?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose ProfilePictureCache failed: {ex}"); }
                try { GlobalFileCache?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose FileCache failed: {ex}"); }

                // Clear in-memory chat cache and any cached chat data used for Toasts
                try
                {
                    GlobalChatCache?.ClearAll();

                    lock (_chatCacheLock)
                    {
                        _chatDataCache.Clear();
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[APP] ClearAll caches failed: {ex}"); }

                // Clear notes cache if any
                try { NotesService?.ClearCache(); } catch (Exception ex) { Debug.WriteLine($"[APP] NotesService.ClearCache failed: {ex}"); }

                // Dispose and clear ChatManager (UI-level) if exists to avoid stale references
                try { GlobalChatManager?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] GlobalChatManager.Dispose failed: {ex}"); }
                GlobalChatManager = null;

                // Build new base
                string apiBase = BaseServerUrlNoCompany;
                if (cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company))
                {
                    apiBase = BaseServerUrlNoCompany.TrimEnd('/') + "/" + cfg.Company.Trim().Trim('/');
                }

                ApiService = new ApiService(apiBase);
                AuthService = new AuthService(apiBase);
                HubService = new HubService(apiBase);

                var cacheScope = string.IsNullOrWhiteSpace(cfg.Company) ? "personal" : cfg.Company.Trim();
                // Ensure profile cache uses live token provider
                GlobalProfilePictureCache = new ProfilePictureCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);
                GlobalFileCache = new FileCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);

                System.Diagnostics.Debug.WriteLine($"[APP] Switched company to '{cfg.Company ?? "(personal)"}', apiBase={apiBase}, cacheScope={cacheScope}");

                // If we already have auth token, set it on ApiService and reconnect hub
                if (!string.IsNullOrEmpty(AuthToken))
                {
                    ApiService.SetAuthToken(AuthToken);
                    _ = HubService.ConnectAsync(AuthToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetCompanyAndApply error: {ex.Message}");
            }
        }

        public static void ExitCompany()
        {
            SetCompanyAndApply(null, false);
        }

        private void GlobalIncomingCallHandler(IncomingCallData data)
        {
            try
            {
                Debug.WriteLine($"[APP] GlobalIncomingCallHandler invoked. callId={data?.CallId} callUid={data?.CallUid} metadata={data?.Metadata}");

                Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // find existing CallWindow or create new one
                        var existing = System.Windows.Application.Current.Windows.OfType<Pages.CallWindow>().FirstOrDefault();
                        if (existing == null)
                        {
                            Debug.WriteLine("[APP] No existing CallWindow found - creating new instance");
                            existing = new Pages.CallWindow();
                            existing.Owner = Current.MainWindow;
                            existing.RegisterHubHandlers();
                            existing.Show();
                            Debug.WriteLine("[APP] New CallWindow shown");
                        }
                        else
                        {
                            Debug.WriteLine("[APP] Found existing CallWindow");
                            // ensure handlers are registered and window visible
                            existing.RegisterHubHandlers();
                            if (!existing.IsVisible)
                            {
                                existing.Owner = Current.MainWindow;
                                existing.Show();
                                Debug.WriteLine("[APP] Existing CallWindow shown");
                            }
                            else
                            {
                                try { existing.Activate(); Debug.WriteLine("[APP] Existing CallWindow activated"); } catch (Exception ex) { Debug.WriteLine($"[APP] Activate failed: {ex}"); }
                            }
                        }

                        try { existing.HandleIncomingCall(data); Debug.WriteLine("[APP] Forwarded incoming call to CallWindow.HandleIncomingCall"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[APP] Failed to forward incoming call to window: {ex}"); }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[APP] GlobalIncomingCallHandler UI error: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] GlobalIncomingCallHandler error: {ex}");
            }
        }
    }
}
