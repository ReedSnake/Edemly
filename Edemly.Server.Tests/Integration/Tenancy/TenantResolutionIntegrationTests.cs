using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Edemly.Contracts.Auth;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Tenancy;

public sealed class TenantResolutionIntegrationTests
{
    [Test]
    public async Task TenantPath_Should_Rewrite_To_Protected_Endpoint_When_Company_ExistsAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        await TestTenantHelper.CreateCompanyAsync(factory, "acme");

        using var response = await client.GetAsync("/acme/api/user/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task TenantPath_Should_Not_Rewrite_When_First_Segment_Does_Not_Match_CompanyAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        await TestTenantHelper.CreateCompanyAsync(factory, "acme");

        using var response = await client.GetAsync("/unknown-tenant/api/user/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TenantPath_Should_Resolve_Company_Case_Insensitively_When_Request_Uses_Tenant_PrefixAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, "allowed@example.test");

        using var response = await client.PostAsJsonAsync(
            "/ACME/api/auth/get-code",
            new LoginRequestDto
            {
                Email = "blocked@example.test"
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(GetMessage(body), Is.EqualTo("Email is not allowed for this company"));
        });
    }

    private static string? GetMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;
    }
}
