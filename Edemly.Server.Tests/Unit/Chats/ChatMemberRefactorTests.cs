using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Edemly.Contracts.ChatMembers;
using Edemly.Server.Api.Controllers.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Edemly.Server.Tests.Unit.Chats;

public sealed class ChatMemberRefactorTests
{
    [Test]
    public async Task Controller_AddMember_Should_Return_Forbid_When_Service_Returns_Forbidden()
    {
        var service = new Mock<IChatMemberService>();
        service.Setup(x => x.AddMember(0, It.IsAny<CreateChatMemberDto>()))
            .ReturnsAsync(ServiceMessageResult.Forbidden());

        var controller = new ChatMemberController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.AddMember(new CreateChatMemberDto { ChatId = 1, UserId = 2, Role = (int)ChatMemberRole.Base });

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Service_AddMember_Should_Return_Forbidden_When_Requester_Has_No_Manage_Rights()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var requester = await CreateUserAsync(serverDb, "requester@example.test");
        var target = await CreateUserAsync(serverDb, "target@example.test");
        var chat = await CreateChatAsync(serverDb);
        await AddMemberAsync(serverDb, chat.Id, requester.Id, ChatMemberRole.Base);

        var service = CreateService(serverDb);
        var result = await service.AddMember(requester.Id, new CreateChatMemberDto
        {
            ChatId = chat.Id,
            UserId = target.Id,
            Role = (int)ChatMemberRole.Base
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Service_AddMember_Should_Not_Create_Duplicate_Membership()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var creator = await CreateUserAsync(serverDb, "creator@example.test");
        var target = await CreateUserAsync(serverDb, "target@example.test");
        var chat = await CreateChatAsync(serverDb);
        await AddMemberAsync(serverDb, chat.Id, creator.Id, ChatMemberRole.Creator);
        await AddMemberAsync(serverDb, chat.Id, target.Id, ChatMemberRole.Base);

        var service = CreateService(serverDb);
        var result = await service.AddMember(creator.Id, new CreateChatMemberDto
        {
            ChatId = chat.Id,
            UserId = target.Id,
            Role = (int)ChatMemberRole.Base
        });

        Assert.That(result.Success, Is.True);
        Assert.That(serverDb.ChatMembers.Count(cm => cm.ChatId == chat.Id && cm.UserId == target.Id), Is.EqualTo(1));
    }

    private static ChatMemberService CreateService(ServerDbContext serverDb)
    {
        return new ChatMemberService(
            serverDb,
            NullLogger<ChatMemberService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());
    }

    private static async Task<User> CreateUserAsync(ServerDbContext serverDb, string email)
    {
        var loginInfo = new LoginInfo { Email = email, IsEmailVerified = true };
        serverDb.LoginInfos.Add(loginInfo);
        await serverDb.SaveChangesAsync();

        var user = new User
        {
            LoginInfoId = loginInfo.Id,
            Username = email.Split('@')[0],
            CreatedAt = DateTime.UtcNow,
            SubscriptionStatus = SubscriptionStatus.Free
        };
        serverDb.Users.Add(user);
        await serverDb.SaveChangesAsync();
        return user;
    }

    private static async Task<Chat> CreateChatAsync(ServerDbContext serverDb)
    {
        var chat = new Chat
        {
            Name = "chat",
            Type = ChatType.Group,
            CreatedAt = DateTime.UtcNow
        };
        serverDb.Chats.Add(chat);
        await serverDb.SaveChangesAsync();
        return chat;
    }

    private static async Task AddMemberAsync(ServerDbContext serverDb, int chatId, int userId, ChatMemberRole role)
    {
        serverDb.ChatMembers.Add(new ChatMember
        {
            ChatId = chatId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        });
        await serverDb.SaveChangesAsync();
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static ServerDbContext CreateServerDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ServerDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private sealed class ThrowingTenantDbContextFactory : ITenantDbContextFactory
    {
        public CompanyDbContext CreateCompanyDbContext(Company company)
        {
            throw new InvalidOperationException("Tenant DB should not be used in this test.");
        }
    }
}
