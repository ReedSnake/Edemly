namespace uchat_server
{
    public interface IPublicUrlProvider
    {
        /// <summary>
        /// Returns configured public base URL (e.g. https://edemly.me) or null if not configured
        /// </summary>
        string? GetPublicBaseUrl();
    }
}
