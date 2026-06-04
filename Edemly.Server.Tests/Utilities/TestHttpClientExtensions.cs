using Edemly.Contracts.Auth;
using System.Net.Http.Headers;

namespace Edemly.Server.Tests.Utilities;

public static class TestHttpClientExtensions
{
    public static HttpClient AddBearerToken(this HttpClient client, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient AddBearerToken(this HttpClient client, AuthResponseDto authResponse)
    {
        ArgumentNullException.ThrowIfNull(authResponse);

        return client.AddBearerToken(authResponse.Token);
    }
}