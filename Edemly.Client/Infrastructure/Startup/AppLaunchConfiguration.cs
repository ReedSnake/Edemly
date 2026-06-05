namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class AppLaunchConfiguration
    {
        public AppLaunchConfiguration(string baseServerUrl, string apiBaseUrl, string cacheScope)
        {
            BaseServerUrl = baseServerUrl;
            ApiBaseUrl = apiBaseUrl;
            CacheScope = cacheScope;
        }

        public string BaseServerUrl { get; }
        public string ApiBaseUrl { get; }
        public string CacheScope { get; }
    }
}
