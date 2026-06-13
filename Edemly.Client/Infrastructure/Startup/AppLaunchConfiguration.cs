namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class AppLaunchConfiguration
    {
        public AppLaunchConfiguration(
            string baseServerUrl,
            string apiBaseUrl,
            string hubServerUrl,
            string cacheScope,
            string updateFeedUrl,
            string clientConfigUrl,
            string selectedServerName,
            AppUpdatePolicy updatePolicy)
        {
            BaseServerUrl = baseServerUrl;
            ApiBaseUrl = apiBaseUrl;
            HubServerUrl = hubServerUrl;
            CacheScope = cacheScope;
            UpdateFeedUrl = updateFeedUrl;
            ClientConfigUrl = clientConfigUrl;
            SelectedServerName = selectedServerName;
            UpdatePolicy = updatePolicy;
        }

        public string BaseServerUrl { get; }
        public string ApiBaseUrl { get; }
        public string HubServerUrl { get; }
        public string CacheScope { get; }
        public string UpdateFeedUrl { get; }
        public string ClientConfigUrl { get; }
        public string SelectedServerName { get; }
        public AppUpdatePolicy UpdatePolicy { get; }
    }
}
