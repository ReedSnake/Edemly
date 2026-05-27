namespace uchat_server
{
    public class PublicUrlProvider : IPublicUrlProvider
    {
        private readonly string? _publicUrl;

        public PublicUrlProvider(string? publicUrl)
        {
            _publicUrl = string.IsNullOrWhiteSpace(publicUrl) ? null : publicUrl.TrimEnd('/');
        }

        public string? GetPublicBaseUrl() => _publicUrl;
    }
}
