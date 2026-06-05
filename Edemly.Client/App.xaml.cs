using CommunityToolkit.WinUI.Notifications;
using Edemly.Client.Api;
using Edemly.Client.Application.Calls;
using Edemly.Client.Application.Chats;
using Edemly.Client.Application.Services;
using Edemly.Client.Application.Session;
using Edemly.Client.Infrastructure.Startup;
using Edemly.Client.Presentation.Controllers.Chats;
using System.Diagnostics;
using System.Windows;
using Edemly.Client.Presentation.Windows;

namespace Edemly.Client
{
    public partial class App : System.Windows.Application
    {
        private const string AppId = "Edemly.MainApp";
        private static readonly ClientUserSession _session = new();
        private static readonly ClientServiceRegistry _serviceRegistry = new(() => Task.FromResult(AuthToken));
        private static Edemly.Client.Presentation.Controls.ConnectionStatusBar? _statusBar;
        private static readonly ChatActivationService _chatActivationService = new(
            () => _serviceRegistry.ApiService,
            () => GlobalChatController,
            () => CurrentUserId,
            EnsureMainWindowAvailable);
        private static readonly ClientSessionManager _sessionManager = new(
            _session,
            _serviceRegistry,
            DisposeGlobalChatController,
            HideStatusBar,
            _chatActivationService.ClearCache);
        private static readonly CallWindowCoordinator _callWindowCoordinator = new(
            () => _serviceRegistry.HubService,
            () => _serviceRegistry.ApiService,
            () => AuthToken,
            () => Current?.MainWindow);
        private static readonly AppRealtimeCoordinator _realtimeCoordinator = new(
            () => _serviceRegistry.HubService,
            () => _serviceRegistry.HubService as HubService,
            () => _statusBar,
            () => AuthToken,
            _callWindowCoordinator.HandleIncomingCall);
        private static readonly CompanyContextSwitcher _companyContextSwitcher = new(
            _serviceRegistry,
            GetBaseServerUrl,
            () => AuthToken,
            _realtimeCoordinator.UnsubscribeHubEvents,
            _realtimeCoordinator.SubscribeHubEvents,
            DisposeGlobalChatController,
            _chatActivationService.ClearCache);

        public static IApiService ApiService => _serviceRegistry.ApiService;
        public static IAuthService AuthService => _serviceRegistry.AuthService;
        public static IHubService HubService => _serviceRegistry.HubService;
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
                _statusBar = value;
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

        public static string? CurrentUsername
        {
            get => _session.UserName;
            set => _session.UserName = value;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppEnvironmentInitializer.ApplySavedPreferences(this, ConfigService.Instance);
            RegisterToastActivationHandler();

            base.OnStartup(e);

            RegisterGlobalExceptionHandlers();

            var launchConfiguration = AppLaunchConfigurationResolver.Resolve(Environment.GetCommandLineArgs(), ConfigService.Instance);
            if (launchConfiguration == null)
            {
                MessageBox.Show(
                    "Server URL is missing.\n\nPlease provide it as the first command-line argument.\nExample:\n    Edemly.exe https://your-server.com",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            Debug.WriteLine($"[APP] Using server URL: {launchConfiguration.BaseServerUrl}");

            BaseServerUrlNoCompany = launchConfiguration.BaseServerUrl;
            _serviceRegistry.Initialize(launchConfiguration.ApiBaseUrl, launchConfiguration.CacheScope);
            SubscribeHubEvents();

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
    }
}
