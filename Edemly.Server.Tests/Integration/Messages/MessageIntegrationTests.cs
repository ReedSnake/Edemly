using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.Messages;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Messages;

public sealed class MessageIntegrationTests
{
    [Test]
    public async Task SendMessage_Should_Create_Message_When_User_Is_Chat_MemberAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await TestAuthHelper.RegisterAsync(senderClient, factory.Services);
        var recipient = await TestAuthHelper.RegisterAsync(recipientClient, factory.Services);
        senderClient.AddBearerToken(sender.JwtToken);
        var chatId = await TestChatHelper.CreatePrivateChatAsync(senderClient, recipient.AuthResponse.UserId);

        using var response = await senderClient.PostAsJsonAsync(
            "/api/message/create",
            new CreateMessageDto
            {
                ChatId = chatId,
                Text = "Hello from integration test",
                Type = (int)MessageType.Txt
            });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var message = await dbContext.Messages.SingleOrDefaultAsync(item => item.ChatId == chatId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(message, Is.Not.Null);
            Assert.That(message!.SenderId, Is.EqualTo(sender.AuthResponse.UserId));
            Assert.That(message.Text, Is.EqualTo("Hello from integration test"));
        });
    }

    [Test]
    public async Task SendMessage_Should_Return_Forbidden_When_User_Is_Not_MemberAsync()
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

        using var response = await strangerClient.PostAsJsonAsync(
            "/api/message/create",
            new CreateMessageDto
            {
                ChatId = chatId,
                Text = "I should not be allowed to post this",
                Type = (int)MessageType.Txt
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetMessages_Should_Return_Messages_In_Correct_OrderAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var senderClient = factory.CreateClient();
        using var recipientClient = factory.CreateClient();
        var sender = await TestAuthHelper.RegisterAsync(senderClient, factory.Services);
        var recipient = await TestAuthHelper.RegisterAsync(recipientClient, factory.Services);
        senderClient.AddBearerToken(sender.JwtToken);
        var chatId = await TestChatHelper.CreatePrivateChatAsync(senderClient, recipient.AuthResponse.UserId);
        await TestChatHelper.SendTextMessageAsync(senderClient, chatId, "first message");
        await Task.Delay(20);
        await TestChatHelper.SendTextMessageAsync(senderClient, chatId, "second message");

        using var response = await senderClient.GetAsync($"/api/message/chat/{chatId}");
        var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(messages, Is.Not.Null);
            Assert.That(messages!.Select(message => message.Text), Is.EqualTo(new[] { "first message", "second message" }));
        });
    }

    [Test]
    public async Task DeleteMessage_Should_Return_Forbidden_When_User_Is_Not_AuthorAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var authorClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var author = await TestAuthHelper.RegisterAsync(authorClient, factory.Services);
        var member = await TestAuthHelper.RegisterAsync(memberClient, factory.Services);
        authorClient.AddBearerToken(author.JwtToken);
        memberClient.AddBearerToken(member.JwtToken);
        var chatId = await TestChatHelper.CreatePrivateChatAsync(authorClient, member.AuthResponse.UserId);
        await TestChatHelper.SendTextMessageAsync(authorClient, chatId, "only the author can delete this");
        var message = await TestChatHelper.GetMessageByTextAsync(factory.Services, chatId, "only the author can delete this");

        using var response = await memberClient.DeleteAsync($"/api/message/delete/{message.Id}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var stillExists = await dbContext.Messages.AnyAsync(item => item.Id == message.Id);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(stillExists, Is.True);
        });
    }
}
