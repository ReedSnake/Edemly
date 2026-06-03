using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.ChatMembers;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Chats;

public sealed class ChatMemberIntegrationTests
{
    [Test]
    public async Task AddChatMember_Should_Add_User_When_Requester_Is_Admin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        using var newUserClient = factory.CreateClient();
        var admin = await TestAuthHelper.RegisterAsync(adminClient, factory.Services);
        var member = await TestAuthHelper.RegisterAsync(memberClient, factory.Services);
        var newUser = await TestAuthHelper.RegisterAsync(newUserClient, factory.Services);
        adminClient.AddBearerToken(admin.JwtToken);
        var chatId = await TestChatHelper.CreateGroupChatAsync(adminClient, "Moderation Team", member.AuthResponse.UserId);

        using var response = await adminClient.PostAsJsonAsync(
            "/api/chatmember/add",
            new CreateChatMemberDto
            {
                ChatId = chatId,
                UserId = newUser.AuthResponse.UserId,
                Role = (int)ChatMemberRole.Base
            });

        var addedMember = await TestChatHelper.GetChatMemberAsync(factory.Services, chatId, newUser.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(addedMember.ChatId, Is.EqualTo(chatId));
            Assert.That(addedMember.UserId, Is.EqualTo(newUser.AuthResponse.UserId));
            Assert.That(addedMember.Role, Is.EqualTo(ChatMemberRole.Base));
        });
    }

    [Test]
    public async Task AddChatMember_Should_Return_Forbidden_When_Requester_Is_Not_Admin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminClient = factory.CreateClient();
        using var baseMemberClient = factory.CreateClient();
        using var newUserClient = factory.CreateClient();
        var admin = await TestAuthHelper.RegisterAsync(adminClient, factory.Services);
        var baseMember = await TestAuthHelper.RegisterAsync(baseMemberClient, factory.Services);
        var newUser = await TestAuthHelper.RegisterAsync(newUserClient, factory.Services);
        adminClient.AddBearerToken(admin.JwtToken);
        baseMemberClient.AddBearerToken(baseMember.JwtToken);
        var chatId = await TestChatHelper.CreateGroupChatAsync(adminClient, "Project Chat", baseMember.AuthResponse.UserId);

        using var response = await baseMemberClient.PostAsJsonAsync(
            "/api/chatmember/add",
            new CreateChatMemberDto
            {
                ChatId = chatId,
                UserId = newUser.AuthResponse.UserId,
                Role = (int)ChatMemberRole.Base
            });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var wasAdded = await dbContext.ChatMembers.AnyAsync(member =>
            member.ChatId == chatId && member.UserId == newUser.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(wasAdded, Is.False);
        });
    }

    [Test]
    public async Task UpdateChatMemberRole_Should_Update_Role_When_Requester_Is_Creator()
    {
        using var factory = new CustomWebApplicationFactory();
        using var creatorClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var creator = await TestAuthHelper.RegisterAsync(creatorClient, factory.Services);
        var member = await TestAuthHelper.RegisterAsync(memberClient, factory.Services);
        creatorClient.AddBearerToken(creator.JwtToken);
        var chatId = await TestChatHelper.CreateGroupChatAsync(creatorClient, "Creators Only", member.AuthResponse.UserId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var creatorMembership = await dbContext.ChatMembers.SingleAsync(chatMember =>
                chatMember.ChatId == chatId && chatMember.UserId == creator.AuthResponse.UserId);

            creatorMembership.Role = ChatMemberRole.Creator;
            await dbContext.SaveChangesAsync();
        }

        var memberToUpdate = await TestChatHelper.GetChatMemberAsync(factory.Services, chatId, member.AuthResponse.UserId);

        using var response = await creatorClient.PutAsJsonAsync(
            "/api/chatmember/update",
            new UpdateChatMemberDto
            {
                Id = memberToUpdate.Id,
                Role = (int)ChatMemberRole.Admin
            });

        var updatedMember = await TestChatHelper.GetChatMemberAsync(factory.Services, chatId, member.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updatedMember.Role, Is.EqualTo(ChatMemberRole.Admin));
        });
    }

    [Test]
    public async Task RemoveChatMember_Should_Remove_User_When_Requester_Is_Admin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var admin = await TestAuthHelper.RegisterAsync(adminClient, factory.Services);
        var member = await TestAuthHelper.RegisterAsync(memberClient, factory.Services);
        adminClient.AddBearerToken(admin.JwtToken);
        var chatId = await TestChatHelper.CreateGroupChatAsync(adminClient, "Review Board", member.AuthResponse.UserId);
        var memberToRemove = await TestChatHelper.GetChatMemberAsync(factory.Services, chatId, member.AuthResponse.UserId);

        using var response = await adminClient.DeleteAsync($"/api/chatmember/delete/{memberToRemove.Id}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var stillExists = await dbContext.ChatMembers.AnyAsync(chatMember => chatMember.Id == memberToRemove.Id);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(stillExists, Is.False);
        });
    }

    [Test]
    public async Task RemoveChatMember_Should_Return_Forbidden_When_Requester_Is_Not_Admin()
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminClient = factory.CreateClient();
        using var baseMemberClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var admin = await TestAuthHelper.RegisterAsync(adminClient, factory.Services);
        var baseMember = await TestAuthHelper.RegisterAsync(baseMemberClient, factory.Services);
        var target = await TestAuthHelper.RegisterAsync(targetClient, factory.Services);
        adminClient.AddBearerToken(admin.JwtToken);
        baseMemberClient.AddBearerToken(baseMember.JwtToken);
        var chatId = await TestChatHelper.CreateGroupChatAsync(
            adminClient,
            "Operations",
            baseMember.AuthResponse.UserId,
            target.AuthResponse.UserId);
        var targetMembership = await TestChatHelper.GetChatMemberAsync(factory.Services, chatId, target.AuthResponse.UserId);

        using var response = await baseMemberClient.DeleteAsync($"/api/chatmember/delete/{targetMembership.Id}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var stillExists = await dbContext.ChatMembers.AnyAsync(chatMember => chatMember.Id == targetMembership.Id);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(stillExists, Is.True);
        });
    }
}
