using Edemly.Contracts.ChatMembers;
using Edemly.Server.Api.Controllers.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Edemly.Server.Tests.Unit.Chats;

public sealed class ChatMemberRefactorTests
{
    [Test]
    public async Task Controller_AddMember_Should_Return_Forbid_When_Service_Returns_ForbiddenAsync()
    {
        var service = new Mock<IChatMemberService>();
        service.Setup(x => x.AddMemberAsync(7, It.IsAny<CreateChatMemberDto>()))
            .ReturnsAsync(ServiceResult.Forbidden());

        var controller = new ChatMemberController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(7)
                }
            }
        };

        var result = await controller.CreateAsync(new CreateChatMemberDto { ChatId = 1, UserId = 2, Role = (int)ChatMemberRole.Base });

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Service_AddMember_Should_Return_Forbidden_When_Requester_Has_No_Manage_RightsAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var requester = await CreateUserAsync(serverDb, "requester@example.test");
        var target = await CreateUserAsync(serverDb, "target@example.test");
        var chat = await CreateChatAsync(serverDb);
        await AddMemberAsync(serverDb, chat.Id, requester.Id, ChatMemberRole.Base);

        var service = CreateService(serverDb);
        var result = await service.AddMemberAsync(requester.Id, new CreateChatMemberDto
        {
            ChatId = chat.Id,
            UserId = target.Id,
            Role = (int)ChatMemberRole.Base
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Service_AddMember_Should_Return_Conflict_When_Membership_Already_ExistsAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var creator = await CreateUserAsync(serverDb, "creator@example.test");
        var target = await CreateUserAsync(serverDb, "target@example.test");
        var chat = await CreateChatAsync(serverDb);
        await AddMemberAsync(serverDb, chat.Id, creator.Id, ChatMemberRole.Creator);
        await AddMemberAsync(serverDb, chat.Id, target.Id, ChatMemberRole.Base);

        var service = CreateService(serverDb);
        var result = await service.AddMemberAsync(creator.Id, new CreateChatMemberDto
        {
            ChatId = chat.Id,
            UserId = target.Id,
            Role = (int)ChatMemberRole.Base
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
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

    private static System.Security.Claims.ClaimsPrincipal TestPrincipal(int userId)
    {
        return new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim("userId", userId.ToString())
                },
                "test"));
    }
}