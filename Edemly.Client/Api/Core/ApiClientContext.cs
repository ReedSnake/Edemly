using System.Net.Http;
using System.Net.Http.Headers;

namespace Edemly.Client.Api.Core;

public sealed class ApiClientContext
{
    private string? _authToken;

    public ApiClientContext(HttpClient httpClient)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public HttpClient HttpClient { get; }

    public void SetBaseUrl(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

        var baseUrl = UrlHelper.NormalizeBaseUrl(serverUrl);
        var baseAddress = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

        HttpClient.BaseAddress = new Uri(baseAddress);
    }

    public void SetAuthToken(string? token)
    {
        _authToken = string.IsNullOrWhiteSpace(token) ? null : token;

        HttpClient.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(_authToken)
                ? null
                : new AuthenticationHeaderValue("Bearer", _authToken);
    }

    public Task<string?> GetValidTokenAsync()
    {
        return Task.FromResult(_authToken);
    }
}