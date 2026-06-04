using CommunityToolkit.WinUI.Notifications;
using Edemly.Client.Api;
using Edemly.Client.Caching;
using Edemly.Client.Pages.Settings;
using Edemly.Client.Realtime;
using Edemly.Client.Services;
using Edemly.Contracts.Realtime;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Edemly.Client
{
    public partial class App : Application
    {
        private const string AppId = "Edemly.MainApp";
        public static IApiService ApiService { get; private set; } = null!;
        public static IAuthService AuthService { get; private set; } = null!;
        public static IHubService HubService { get; private set; } = null!;
        public static NotesService? NotesService { get; private set; }

        public static ChatCache GlobalChatCache { get; private set; } = new ChatCache();
        public static ProfilePictureCache GlobalProfilePictureCache { get; private set; } = null!;
        public static FileCache GlobalFileCache { get; private set; } = null!;

        public static ChatManager? GlobalChatManager { get; set; }

        public static Edemly.Client.Controls.ConnectionStatusBar? StatusBar { get; set; }

        public static int? CurrentUserId { get; set; }
        public static string? CurrentUserEmail { get; set; }
        public static string? CurrentUserName { get; set; }
        public static string? CurrentUserPhotoUrl { get; set; }
        public static string? AuthToken { get; set; }

        private static readonly Dictionary<int, (ChatDto chat, List<ChatMemberDto> members)> _chatDataCache = new();
        private static readonly object _chatCacheLock = new object();

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

                try
                {
                    LanguageService.Instance.LoadLanguage(savedLang);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] LoadLanguage failed: {ex}");
                }

                try
                {
                    ThemeService.Instance.LoadAndApplyTheme();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP] LoadAndApplyTheme failed: {ex}");
                }

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

            string? serverUrl = null;
            string? tenantArg = null;

            try
            {
                var argsList = Environment.GetCommandLineArgs();

                if (argsList.Length > 1)
                {
                    for (int i = 1; i < argsList.Length; i++)
                    {
                        var raw = argsList[i].Trim();
                        if (string.IsNullOrEmpty(raw)) continue;

                        if (raw.StartsWith("--") || raw.StartsWith("-"))
                        {
                            if (raw.StartsWith("--tenant", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("--company", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = raw.Split(new[] { '=' }, 2);
                                if (parts.Length == 2)
                                {
                                    tenantArg = parts[1].Trim().Trim('"');
                                }
                                else
                                {
                                    if (i + 1 < argsList.Length)
                                    {
                                        tenantArg = argsList[i + 1].Trim().Trim('"');
                                        i++; // consume
                                    }
                                }

                                continue;
                            }

                            continue;
                        }

                        if (serverUrl == null)
                        {
                            var candidate = raw;

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

            BaseServerUrlNoCompany = serverUrl!; // save base

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

            var cfg2 = ConfigService.Instance;
            string apiBase = BaseServerUrlNoCompany;
            if (cfg2.IsInstalled && !string.IsNullOrWhiteSpace(cfg2.Company) && !string.Equals(cfg2.Company, "Personal", StringComparison.OrdinalIgnoreCase))
            {
                apiBase = BaseServerUrlNoCompany.TrimEnd('/') + "/" + cfg2.Company.Trim().Trim('/');
            }

            ApiService = new ApiService(apiBase);
            AuthService = new AuthService(apiBase);
            HubService = new HubService(apiBase);

            try
            {
                var concreteHub = (((HubService as HubService)));
                if (concreteHub != null)
                {
                    concreteHub.IncomingCallReceived += GlobalIncomingCallHandler;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to subscribe incoming call handler: {ex}");
            }

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

            var cacheScope = string.IsNullOrWhiteSpace(cfg2.Company) ? "personal" : cfg2.Company.Trim();
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

            try
            {
                var mainWindow = new MainWindow();
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to create MainWindow: {ex}");
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
                        bool isReconnecting = false;
                        try
                        {
                            var concrete = (((HubService as HubService)));
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

                var userInfo = await ApiService.GetUserInfoAsync();
                if (userInfo == null || userInfo.Id <= 0)
                {
                    return;
                }

                CurrentUserPhotoUrl = userInfo.PfpUrl;
                MyInfo.UserName = userInfo.Username ?? string.Empty;
                MyInfo.Email = userInfo.Email ?? string.Empty;
                MyInfo.PhoneNumber = userInfo.PhoneNumber ?? string.Empty;
                MyInfo.PfpUrl = userInfo.PfpUrl ?? string.Empty;
                MyInfo.Description = userInfo.Description ?? string.Empty;
                MyInfo.FirstName = userInfo.FirstName ?? string.Empty;
                MyInfo.LastName = userInfo.LastName ?? string.Empty;

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
                int waited = 0;
                while (HubService == null && waited < 5000)
                {
                    await Task.Delay(100);
                    waited += 100;
                }

                if (HubService == null) return;

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

                try
                {
                    var calls = await ApiService.GetActiveCallsAsync();
                    if (calls != null && calls.Count > 0)
                    {
                        Current.Dispatcher.Invoke(() =>
                        {
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

                ChatDto? chat;
                List<ChatMemberDto>? members = null;

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
                                ? "pack://application:,,,/Assets/Avatars/default-avatar.png"
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
                        ? "pack://application:,,,/Assets/Avatars/default-avatar.png"
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

                try { (HubService as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose HubService failed: {ex}"); }
                try { (ApiService as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose ApiService failed: {ex}"); }

                try { GlobalProfilePictureCache?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose ProfilePictureCache failed: {ex}"); }
                try { GlobalFileCache?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] Dispose FileCache failed: {ex}"); }

                try
                {
                    GlobalChatCache?.ClearAll();

                    lock (_chatCacheLock)
                    {
                        _chatDataCache.Clear();
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[APP] ClearAll caches failed: {ex}"); }

                try { NotesService?.ClearCache(); } catch (Exception ex) { Debug.WriteLine($"[APP] NotesService.ClearCache failed: {ex}"); }

                try { GlobalChatManager?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[APP] GlobalChatManager.Dispose failed: {ex}"); }
                GlobalChatManager = null;

                string apiBase = BaseServerUrlNoCompany;
                if (cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company))
                {
                    apiBase = BaseServerUrlNoCompany.TrimEnd('/') + "/" + cfg.Company.Trim().Trim('/');
                }

                ApiService = new ApiService(apiBase);
                AuthService = new AuthService(apiBase);
                HubService = new HubService(apiBase);

                var cacheScope = string.IsNullOrWhiteSpace(cfg.Company) ? "personal" : cfg.Company.Trim();
                GlobalProfilePictureCache = new ProfilePictureCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);
                GlobalFileCache = new FileCache(apiBase, () => Task.FromResult(AuthToken), cacheScope);

                System.Diagnostics.Debug.WriteLine($"[APP] Switched company to '{cfg.Company ?? "(personal)"}', apiBase={apiBase}, cacheScope={cacheScope}");

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

        private void GlobalIncomingCallHandler(IncomingCallEventDto data)
        {
            try
            {
                Debug.WriteLine($"[APP] GlobalIncomingCallHandler invoked. callId={data?.CallId} callUid={data?.CallUid} metadata={data?.Metadata}");

                Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
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