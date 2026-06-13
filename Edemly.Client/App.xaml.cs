using CommunityToolkit.WinUI.Notifications;
using Edemly.Client.Application.Calls;
using Edemly.Client.Application.Chats;
using Edemly.Client.Application.Session;
using Edemly.Client.Infrastructure.Startup;
using Edemly.Client.Presentation.Controllers.Chats;
using System.Diagnostics;
using System.Windows;
using Edemly.Client.Presentation.Windows;
using Edemly.Client.Api;
using Edemly.Client.Infrastructure.Realtime;
using Edemly.Client.Infrastructure.Caching;
using Edemly.Client.Infrastructure.Storage;
using Edemly.Client.Application.Auth;
using Edemly.Client.Application.Notes;
using Velopack;

namespace Edemly.Client
{
    public partial class App : System.Windows.Application
    {
        private const string AppId = "Edemly.MainApp";
        private static readonly ClientUserSession _session = new();
        private static readonly ClientServiceRegistry _serviceRegistry = new(() => Task.FromResult(AuthToken));
        private static Edemly.Client.Presentation.Controls.ConnectionStatusBar? _statusBar;
        private static AppUpdateCheckResult? _pendingUpdate;
        private static int _updateInstallStarted;
        private static readonly ChatActivationService _chatActivationService = new(
            () => _serviceRegistry.ApiClients,
            () => GlobalChatController,
            () => CurrentUserId,
            EnsureMainWindowAvailable);
        private static readonly CallSessionState _callSessionState = new();
        private static readonly CallSessionController _callSessionController = new(
            _callSessionState,
            () => _serviceRegistry.HubService,
            () => CurrentUserId);
        private static readonly ClientSessionManager _sessionManager = new(
            _session,
            _serviceRegistry,
            DisposeGlobalChatController,
            HideStatusBar,
            _chatActivationService.ClearCache);
        private static readonly CallWindowCoordinator _callWindowCoordinator = new(
            () => _serviceRegistry.HubService,
            () => AuthToken,
            () => Current?.MainWindow,
            _callSessionController);
        private static readonly AppRealtimeCoordinator _realtimeCoordinator = new(
            () => _serviceRegistry.HubService,
            () => _serviceRegistry.HubService as HubService,
            () => _statusBar,
            () => AuthToken,
            _callWindowCoordinator.HandleIncomingCall);
        private static readonly CompanyContextSwitcher _companyContextSwitcher = new(
            _serviceRegistry,
            GetBaseServerUrl,
            GetHubServerUrl,
            () => AuthToken,
            _realtimeCoordinator.UnsubscribeHubEvents,
            _realtimeCoordinator.SubscribeHubEvents,
            DisposeGlobalChatController,
            _chatActivationService.ClearCache);

        public static IApiClients ApiClients => _serviceRegistry.ApiClients;
        public static IAuthService AuthService => _serviceRegistry.AuthService;
        public static IHubService HubService => _serviceRegistry.HubService;
        public static CallSessionState CallSessionState => _callSessionState;
        public static CallSessionController CallSessionController => _callSessionController;
        public static NotesService? NotesService => _serviceRegistry.NotesService;

        public static ChatCache GlobalChatCache => _serviceRegistry.ChatCache;
        public static ProfilePictureCache GlobalProfilePictureCache => _serviceRegistry.ProfilePictureCache;
        public static FileCache GlobalFileCache => _serviceRegistry.FileCache;

        public static ChatWorkspaceController? GlobalChatController { get; set; }

        public static Edemly.Client.Presentation.Controls.ConnectionStatusBar? StatusBar
        {
            get => _statusBar;
            set
            {
                DetachUpdateStatusBarHandlers(_statusBar);
                _statusBar = value;
                AttachUpdateStatusBarHandlers(_statusBar);
                _realtimeCoordinator.RefreshConnectionState();
            }
        }

        public static int? CurrentUserId
        {
            get => _session.UserId;
            set => _session.UserId = value;
        }

        public static string? CurrentUserEmail
        {
            get => _session.Email;
            set => _session.Email = value;
        }

        public static string? CurrentUserName
        {
            get => _session.UserName;
            set => _session.UserName = value;
        }

        public static string? CurrentUserPhotoUrl
        {
            get => _session.PhotoUrl;
            set => _session.PhotoUrl = value;
        }

        public static string? AuthToken
        {
            get => _session.AuthToken;
            set => _session.AuthToken = value;
        }

        public static string BaseServerUrlNoCompany { get; private set; } = string.Empty;
        public static string HubServerUrlNoCompany { get; private set; } = string.Empty;

        public static string? CurrentUsername
        {
            get => _session.UserName;
            set => _session.UserName = value;
        }
        [STAThread]
        private static void Main(string[] args)
        {
            VelopackApp.Build()
                .SetAppUserModelId(AppId)
                .Run();
            App app = new();
            app.InitializeComponent();
            app.Run();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            AppEnvironmentInitializer.ApplySavedPreferences(this, ConfigService.Instance);
            RegisterToastActivationHandler();

            base.OnStartup(e);

            RegisterGlobalExceptionHandlers();

            var launchConfiguration = await AppLaunchConfigurationResolver.ResolveAsync(
                Environment.GetCommandLineArgs(),
                ConfigService.Instance);
            if (launchConfiguration == null)
            {
                MessageBox.Show(
                    "Server configuration is missing.\n\nStart the local static site or provide a server URL.\nExamples:\n    Edemly.exe http://localhost:3500\n    Edemly.exe --config-url http://localhost:8080/client.json",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            Debug.WriteLine($"[APP] Using server URL: {launchConfiguration.BaseServerUrl}");
            Debug.WriteLine($"[APP] Using hub server URL: {launchConfiguration.HubServerUrl}");
            Debug.WriteLine($"[APP] Selected server: {launchConfiguration.SelectedServerName}");
            Debug.WriteLine($"[APP] Client config URL: {launchConfiguration.ClientConfigUrl}");
            Debug.WriteLine($"[APP] Update feed URL: {launchConfiguration.UpdateFeedUrl}");

            BaseServerUrlNoCompany = launchConfiguration.BaseServerUrl;
            HubServerUrlNoCompany = launchConfiguration.HubServerUrl;
            _serviceRegistry.Initialize(
                launchConfiguration.ApiBaseUrl,
                launchConfiguration.HubServerUrl,
                launchConfiguration.CacheScope);
            SubscribeHubEvents();

            try
            {
                var mainWindow = new MainWindow();
                Current.MainWindow = mainWindow;
                mainWindow.Show();
                StartAutoUpdateCheck(launchConfiguration.UpdateFeedUrl, launchConfiguration.UpdatePolicy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to create MainWindow: {ex}");
                Shutdown();
            }
        }

        private void RegisterToastActivationHandler()
        {
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var args = ToastArguments.Parse(toastArgs.Argument);

                        if (args.TryGetValue("action", out string action)
                            && action == "viewChat"
                            && args.TryGetValue("chatId", out string chatIdStr)
                            && int.TryParse(chatIdStr, out int chatId))
                        {
                            OpenChatWindow(chatId);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error in Toast: {ex.Message}");
                    }
                });
            };
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += (sender, args) =>
            {
                try
                {
                    Debug.WriteLine($"[APP][UNHANDLED] DispatcherUnhandledException: {args.Exception}");
                    MessageBox.Show(
                        $"Unhandled UI exception: {args.Exception.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP][UNHANDLED] Failed to show DispatcherUnhandledException: {ex}");
                }
                finally
                {
                    args.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                try
                {
                    var ex = args.ExceptionObject as Exception;
                    Debug.WriteLine($"[APP][UNHANDLED] DomainUnhandledException: {ex}");
                    Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                $"Fatal error: {ex?.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        catch (Exception ex2)
                        {
                            Debug.WriteLine($"[APP][UNHANDLED] Failed to show fatal error dialog: {ex2}");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP][UNHANDLED] Domain handler failed: {ex}");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                try
                {
                    Debug.WriteLine($"[APP][UNOBSERVED] TaskScheduler.UnobservedTaskException: {args.Exception}");
                    args.SetObserved();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP][UNOBSERVED] Failed to handle unobserved task exception: {ex}");
                }
            };
        }

        private void SubscribeHubEvents()
        {
            _realtimeCoordinator.SubscribeHubEvents();
        }

        private void UnsubscribeHubEvents()
        {
            _realtimeCoordinator.UnsubscribeHubEvents();
        }

        public static void SetCurrentUser(int userId, string email, string username, string? photoUrl = null, string? token = null)
        {
            _sessionManager.SetCurrentUser(userId, email, username, photoUrl, token);
        }

        public static Task RefreshCurrentUserProfileAsync()
        {
            return _sessionManager.RefreshCurrentUserProfileAsync();
        }

        public static Task EnsureHubConnectedAndRestoreCallsAsync()
        {
            return _callWindowCoordinator.EnsureHubConnectedAndRestoreCallsAsync();
        }

        public static void ClearCurrentUser()
        {
            _sessionManager.ClearCurrentUser();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ToastNotificationManagerCompat.History.Clear();
            ToastNotificationManagerCompat.Uninstall();

            UnsubscribeHubEvents();
            _serviceRegistry.Dispose();

            base.OnExit(e);
        }

        private static void OpenChatWindow(int chatId)
        {
            _ = _chatActivationService.OpenChatWindowAsync(chatId);
        }

        public static void SetCompanyAndApply(string? company, bool markInstalled)
        {
            _companyContextSwitcher.SetCompanyAndApply(company, markInstalled);
        }

        public static void ExitCompany()
        {
            _companyContextSwitcher.ExitCompany();
        }

        private static MainWindow EnsureMainWindowAvailable()
        {
            var mainWindow = Current?.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                return mainWindow;
            }

            mainWindow = new MainWindow();
            if (Current != null)
            {
                Current.MainWindow = mainWindow;
            }

            mainWindow.Show();
            return mainWindow;
        }

        private static void DisposeGlobalChatController()
        {
            try
            {
                GlobalChatController?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] GlobalChatController.Dispose failed: {ex}");
            }
            finally
            {
                GlobalChatController = null;
            }
        }

        private static void HideStatusBar()
        {
            Current?.Dispatcher?.Invoke(() => _statusBar?.Hide());
        }

        private static string GetBaseServerUrl()
        {
            return BaseServerUrlNoCompany ?? string.Empty;
        }

        private static string GetHubServerUrl()
        {
            return string.IsNullOrWhiteSpace(HubServerUrlNoCompany)
                ? GetBaseServerUrl()
                : HubServerUrlNoCompany;
        }

        private static void StartAutoUpdateCheck(string updateFeedUrl, AppUpdatePolicy updatePolicy)
        {
            if (string.IsNullOrWhiteSpace(updateFeedUrl))
            {
                Debug.WriteLine("[APP UPDATE] Update feed URL is empty. Auto-update check skipped.");
                return;
            }

            _ = Task.Run(async () =>
            {
                var update = await AppUpdateService.CheckForUpdateAsync(updateFeedUrl, updatePolicy);
                if (!update.HasUpdate)
                {
                    Debug.WriteLine($"[APP UPDATE] {update.StatusMessage}");
                    return;
                }

                _pendingUpdate = update;
                await ShowPendingUpdateAsync(update);

                if (update.IsMandatory)
                {
                    await InstallPendingUpdateAsync(update);
                }
            });
        }

        private static void AttachUpdateStatusBarHandlers(Edemly.Client.Presentation.Controls.ConnectionStatusBar? statusBar)
        {
            if (statusBar == null)
            {
                return;
            }

            statusBar.UpdateNowRequested -= OnUpdateNowRequested;
            statusBar.UpdateNowRequested += OnUpdateNowRequested;
        }

        private static void DetachUpdateStatusBarHandlers(Edemly.Client.Presentation.Controls.ConnectionStatusBar? statusBar)
        {
            if (statusBar == null)
            {
                return;
            }

            statusBar.UpdateNowRequested -= OnUpdateNowRequested;
        }

        private static void OnUpdateNowRequested(object? sender, EventArgs e)
        {
            _ = InstallPendingUpdateAsync(_pendingUpdate);
        }

        private static async Task ShowPendingUpdateAsync(AppUpdateCheckResult update)
        {
            var dispatcher = Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                _statusBar?.ShowUpdateAvailable(update.Version, update.IsMandatory);
            });
        }

        private static async Task InstallPendingUpdateAsync(AppUpdateCheckResult? update)
        {
            if (update == null || !update.HasUpdate)
            {
                return;
            }

            if (System.Threading.Interlocked.Exchange(ref _updateInstallStarted, 1) == 1)
            {
                return;
            }

            try
            {
                await ShowUpdateInstallingAsync(update, null);
                await AppUpdateService.DownloadAndApplyUpdateAsync(
                    update,
                    progress => _ = ShowUpdateInstallingAsync(update, progress));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP UPDATE] Failed to install update: {ex}");
                await ShowUpdateFailedAsync(update, ex.Message);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _updateInstallStarted, 0);
            }
        }

        private static async Task ShowUpdateInstallingAsync(AppUpdateCheckResult update, int? progress)
        {
            var dispatcher = Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                _statusBar?.ShowUpdateInstalling(update.Version, progress, update.IsMandatory);
            });
        }

        private static async Task ShowUpdateFailedAsync(AppUpdateCheckResult update, string message)
        {
            var dispatcher = Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                _statusBar?.ShowUpdateFailed(message, update.IsMandatory);
            });
        }
    }
}
