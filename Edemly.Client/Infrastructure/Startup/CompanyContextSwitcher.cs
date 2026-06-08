using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class CompanyContextSwitcher
    {
        private readonly ClientServiceRegistry _serviceRegistry;
        private readonly Func<string> _baseServerUrlProvider;
        private readonly Func<string?> _authTokenProvider;
        private readonly Action _unsubscribeHubEvents;
        private readonly Action _subscribeHubEvents;
        private readonly Action _disposeChatManager;
        private readonly Action _clearChatActivationCache;

        public CompanyContextSwitcher(
            ClientServiceRegistry serviceRegistry,
            Func<string> baseServerUrlProvider,
            Func<string?> authTokenProvider,
            Action unsubscribeHubEvents,
            Action subscribeHubEvents,
            Action disposeChatManager,
            Action clearChatActivationCache)
        {
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _baseServerUrlProvider = baseServerUrlProvider ?? throw new ArgumentNullException(nameof(baseServerUrlProvider));
            _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
            _unsubscribeHubEvents = unsubscribeHubEvents ?? throw new ArgumentNullException(nameof(unsubscribeHubEvents));
            _subscribeHubEvents = subscribeHubEvents ?? throw new ArgumentNullException(nameof(subscribeHubEvents));
            _disposeChatManager = disposeChatManager ?? throw new ArgumentNullException(nameof(disposeChatManager));
            _clearChatActivationCache = clearChatActivationCache ?? throw new ArgumentNullException(nameof(clearChatActivationCache));
        }

        public void SetCompanyAndApply(string? company, bool markInstalled)
        {
            try
            {
                var config = ConfigService.Instance;
                if (string.IsNullOrWhiteSpace(company) || string.Equals(company, "Personal", StringComparison.OrdinalIgnoreCase))
                {
                    config.Company = string.Empty;
                }
                else
                {
                    config.Company = company.Trim();
                }

                config.IsInstalled = markInstalled;
                config.Save();

                _unsubscribeHubEvents();

                _serviceRegistry.DisposeCoreServices();
                _serviceRegistry.DisposeMediaCaches();

                try
                {
                    _serviceRegistry.ClearConversationState();
                    _clearChatActivationCache();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[COMPANY SWITCH] Failed to clear caches: {ex}");
                }

                try
                {
                    _disposeChatManager();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[COMPANY SWITCH] Dispose chat manager failed: {ex}");
                }

                var apiBase = _baseServerUrlProvider();
                if (config.IsInstalled && !string.IsNullOrWhiteSpace(config.Company))
                {
                    apiBase = _baseServerUrlProvider().TrimEnd('/') + "/" + config.Company.Trim().Trim('/');
                }

                var cacheScope = string.IsNullOrWhiteSpace(config.Company) ? "personal" : config.Company.Trim();
                _serviceRegistry.Initialize(apiBase, cacheScope);

                Debug.WriteLine(
                    $"[COMPANY SWITCH] Switched company to '{config.Company ?? "(personal)"}', apiBase={apiBase}, cacheScope={cacheScope}");

                _subscribeHubEvents();

                var authToken = _authTokenProvider();
                if (!string.IsNullOrEmpty(authToken))
                {
                    _serviceRegistry.SetAuthToken(authToken);
                    _ = _serviceRegistry.HubService.ConnectAsync(authToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[COMPANY SWITCH] SetCompanyAndApply failed: {ex.Message}");
            }
        }

        public void ExitCompany()
        {
            SetCompanyAndApply(null, markInstalled: false);
        }
    }
}
