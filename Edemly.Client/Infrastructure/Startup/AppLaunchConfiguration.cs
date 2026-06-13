namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class AppLaunchConfiguration
    {
        public AppLaunchConfiguration(string baseServerUrl, string apiBaseUrl, string hubServerUrl, string cacheScope)
        {
            BaseServerUrl = baseServerUrl;
            ApiBaseUrl = apiBaseUrl;
            HubServerUrl = hubServerUrl;
            CacheScope = cacheScope;
        }

        public string BaseServerUrl { get; }
        public string ApiBaseUrl { get; }
        public string HubServerUrl { get; }
        public string CacheScope { get; }
    }
}
