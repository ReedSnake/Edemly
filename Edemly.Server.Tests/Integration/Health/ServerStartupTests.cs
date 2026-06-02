using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Server.Data;
using Edemly.Server.Tests.Infrastructure;

namespace Edemly.Server.Tests.Integration.Health;

public sealed class ServerStartupTests
{
    [Test]
    public async Task Server_Should_Start()
    {
        using var factory = new CustomWebApplicationFactory();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        Assert.That(dbContext.Database.ProviderName, Is.EqualTo("Microsoft.EntityFrameworkCore.Sqlite"));
        Assert.That(await dbContext.Database.CanConnectAsync(), Is.True);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");

        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError));
    }
}
