using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.Chats;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Chats;

public sealed class ChatIntegrationTests
{
    [Test]
    public async Task CreatePrivateChat_Should_Create_Chat_When_Users_Exist()
    {
        using var factory = new CustomWebApplicationFactory();
        using var userClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        var currentUser = await TestAuthHelper.RegisterAsync(userClient, factory.Services);
        var otherUser = await TestAuthHelper.RegisterAsync(otherClient, factory.Services);
        userClient.AddBearerToken(currentUser.JwtToken);

        var chatId = await TestChatHelper.CreatePrivateChatAsync(userClient, otherUser.AuthResponse.UserId);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var chat = await dbContext.Chats
            .Include(item => item.ChatMembers)
            .SingleOrDefaultAsync(item => item.Id == chatId);

        Assert.Multiple(() =>
        {
            Assert.That(chat, Is.Not.Null);
            Assert.That(chat!.Type, Is.EqualTo(ChatType.Direct));
            Assert.That(chat.ChatMembers.Select(member => member.UserId), Does.Contain(currentUser.AuthResponse.UserId));
            Assert.That(chat.ChatMembers.Select(member => member.UserId), Does.Contain(otherUser.AuthResponse.UserId));
        });
    }

    [Test]
    public async Task CreatePrivateChat_Should_Return_Unauthorized_Without_Token()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/chat/create-private",
            new CreatePrivateChatDto { UserId = 1 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetMyChats_Should_Return_Only_User_Chats()
    {
        using var factory = new CustomWebApplicationFactory();
        using var user1Client = factory.CreateClient();
        using var user2Client = factory.CreateClient();
        using var user3Client = factory.CreateClient();
        var user1 = await TestAuthHelper.RegisterAsync(user1Client, factory.Services);
        var user2 = await TestAuthHelper.RegisterAsync(user2Client, factory.Services);
        var user3 = await TestAuthHelper.RegisterAsync(user3Client, factory.Services);
        user1Client.AddBearerToken(user1.JwtToken);
        user2Client.AddBearerToken(user2.JwtToken);
        var user1ChatId = await TestChatHelper.CreatePrivateChatAsync(user1Client, user2.AuthResponse.UserId);
        var unrelatedChatId = await TestChatHelper.CreatePrivateChatAsync(user2Client, user3.AuthResponse.UserId);

        using var response = await user1Client.GetAsync("/api/chat/my-chats");
        var chats = await response.Content.ReadFromJsonAsync<List<ChatDto>>();
        var chatIds = chats?.Select(chat => chat.Id).ToList() ?? new List<int>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(chats, Is.Not.Null);
            Assert.That(chatIds, Does.Contain(user1ChatId));
            Assert.That(chatIds, Does.Not.Contain(unrelatedChatId));
        });
    }

    [Test]
    public async Task GetChat_Should_Return_Forbidden_When_User_Is_Not_Member()
    {
        using var factory = new CustomWebApplicationFactory();
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        using var strangerClient = factory.CreateClient();
        var owner = await TestAuthHelper.RegisterAsync(ownerClient, factory.Services);
        var member = await TestAuthHelper.RegisterAsync(memberClient, factory.Services);
        var stranger = await TestAuthHelper.RegisterAsync(strangerClient, factory.Services);
        ownerClient.AddBearerToken(owner.JwtToken);
        strangerClient.AddBearerToken(stranger.JwtToken);
        var chatId = await TestChatHelper.CreatePrivateChatAsync(ownerClient, member.AuthResponse.UserId);

        using var response = await strangerClient.GetAsync($"/api/chat/{chatId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
