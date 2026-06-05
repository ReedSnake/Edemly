namespace Edemly.Client.Api;

public static class UrlHelper
{
    public static string NormalizeBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        return url.TrimEnd('/');
    }

    public static string BuildRelativeUrl(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return relativeOrAbsolute ?? string.Empty;

        if (Uri.IsWellFormedUriString(relativeOrAbsolute, UriKind.Absolute))
            return relativeOrAbsolute;

        return relativeOrAbsolute.TrimStart('/');
    }

    public static string BuildHubUrl(string baseUrl, string hubPath, string? tenant = null)
    {
        var url = NormalizeBaseUrl(baseUrl);

        if (!hubPath.StartsWith('/'))
            hubPath = "/" + hubPath;

        url += hubPath;

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            url += "?tenant=" + Uri.EscapeDataString(tenant);
        }

        return url;
    }
}
