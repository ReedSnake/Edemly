using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.Users;
using Edemly.Server.Data;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Users;

public sealed class UserIntegrationTests
{
    [Test]
    public async Task GetMe_Should_Return_Current_User_When_Token_Is_ValidAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.GetAsync("/api/user/me");
        var body = await response.Content.ReadFromJsonAsync<GetMeResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.User.Id, Is.EqualTo(session.AuthResponse.UserId));
            Assert.That(body.User.Email, Is.EqualTo(session.User.Email));
            Assert.That(body.User.Username, Is.EqualTo(session.AuthResponse.Username));
        });
    }

    [Test]
    public async Task GetMe_Should_Return_Unauthorized_Without_TokenAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/user/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SearchUsers_Should_Return_Matching_UsersAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var searcherClient = factory.CreateClient();
        using var matchingUserClient = factory.CreateClient();
        using var unrelatedUserClient = factory.CreateClient();
        var searcher = await TestAuthHelper.RegisterAsync(searcherClient, factory.Services);
        var matchingUser = await TestAuthHelper.RegisterAsync(matchingUserClient, factory.Services);
        var unrelatedUser = await TestAuthHelper.RegisterAsync(unrelatedUserClient, factory.Services);
        searcherClient.AddBearerToken(searcher.JwtToken);
        var uniqueQuery = matchingUser.User.Email.Split('@')[0].Split('-')[2];

        using var response = await searcherClient.GetAsync($"/api/user/search?query={uniqueQuery}");
        var body = await response.Content.ReadFromJsonAsync<SearchUsersResponse>();
        var userIds = body?.Users.Select(user => user.Id).ToList() ?? new List<int>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(userIds, Does.Contain(matchingUser.AuthResponse.UserId));
            Assert.That(userIds, Does.Not.Contain(unrelatedUser.AuthResponse.UserId));
        });
    }

    [Test]
    public async Task SearchUsers_Should_Return_Unauthorized_Without_TokenAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/user/search?query=test-user");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UpdateProfile_Should_Update_User_Data_When_Request_Is_ValidAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.PutAsJsonAsync(
            "/api/user/update",
            new UpdateUserDto
            {
                Username = "updateduser123",
                FirstName = "Test",
                LastName = "User",
                PhoneNumber = "380991112233",
                Location = "Kyiv",
                Description = "Updated from integration test",
                PfpUrl = "https://cdn.example.test/profile.png"
            });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(user.Username, Is.EqualTo("updateduser123"));
            Assert.That(user.FirstName, Is.EqualTo("Test"));
            Assert.That(user.LastName, Is.EqualTo("User"));
            Assert.That(user.PhoneNumber, Is.EqualTo("380991112233"));
            Assert.That(user.Location, Is.EqualTo("Kyiv"));
            Assert.That(user.Description, Is.EqualTo("Updated from integration test"));
            Assert.That(user.PfpUrl, Is.EqualTo("https://cdn.example.test/profile.png"));
        });
    }

    [Test]
    public async Task UpdateProfile_Should_Clear_Optional_Fields_When_Empty_Strings_Are_ProvidedAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using (var initialResponse = await client.PutAsJsonAsync(
                   "/api/user/update",
                   new UpdateUserDto
                   {
                       Username = "clearableuser",
                       FirstName = "Temp",
                       LastName = "User"
                   }))
        {
            Assert.That(initialResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using var response = await client.PutAsJsonAsync(
            "/api/user/update",
            new UpdateUserDto
            {
                Username = string.Empty,
                FirstName = string.Empty,
                LastName = string.Empty
            });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(user.Username, Is.Null);
            Assert.That(user.FirstName, Is.Null);
            Assert.That(user.LastName, Is.Null);
        });
    }

    [Test]
    public async Task UpdateProfile_Should_Return_Conflict_When_Username_Is_DuplicateAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstSession = await TestAuthHelper.RegisterAsync(firstClient, factory.Services);
        var secondSession = await TestAuthHelper.RegisterAsync(secondClient, factory.Services);
        firstClient.AddBearerToken(firstSession.JwtToken);
        secondClient.AddBearerToken(secondSession.JwtToken);

        using (var firstResponse = await firstClient.PutAsJsonAsync(
                   "/api/user/update",
                   new UpdateUserDto { Username = "shared-update-user" }))
        {
            Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using var response = await secondClient.PutAsJsonAsync(
            "/api/user/update",
            new UpdateUserDto { Username = "shared-update-user" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task UpdateProfile_Should_Return_Unauthorized_Without_TokenAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/user/update",
            new UpdateUserDto
            {
                FirstName = "Unauthorized"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeleteUser_Should_Remove_Current_UserAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.DeleteAsync($"/api/user/delete?targetUserId={session.AuthResponse.UserId}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var userExists = await dbContext.Users.AnyAsync(item => item.Id == session.AuthResponse.UserId);
        var sessionExists = await dbContext.Sessions.AnyAsync(item => item.UserId == session.AuthResponse.UserId);
        var membershipExists = await dbContext.ChatMembers.AnyAsync(item => item.UserId == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(userExists, Is.False);
            Assert.That(sessionExists, Is.False);
            Assert.That(membershipExists, Is.False);
        });
    }

    [Test]
    public async Task DeleteUser_Should_Return_Forbidden_When_Deleting_Another_UserAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var ownerClient = factory.CreateClient();
        using var otherUserClient = factory.CreateClient();
        var owner = await TestAuthHelper.RegisterAsync(ownerClient, factory.Services);
        var otherUser = await TestAuthHelper.RegisterAsync(otherUserClient, factory.Services);
        ownerClient.AddBearerToken(owner.JwtToken);

        using var response = await ownerClient.DeleteAsync($"/api/user/delete?targetUserId={otherUser.AuthResponse.UserId}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var userExists = await dbContext.Users.AnyAsync(item => item.Id == otherUser.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(userExists, Is.True);
        });
    }

    [Test]
    public async Task DeleteUser_Should_Return_Unauthorized_Without_TokenAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync("/api/user/delete?targetUserId=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private sealed class GetMeResponse
    {
        public UserInfoDto User { get; set; } = null!;
    }

    private sealed class SearchUsersResponse
    {
        public List<UserDto> Users { get; set; } = new();
        public int Count { get; set; }
    }
}
