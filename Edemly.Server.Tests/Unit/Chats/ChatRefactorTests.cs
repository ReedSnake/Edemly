using Edemly.Contracts.Chats;
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
using System.Security.Claims;

namespace Edemly.Server.Tests.Unit.Chats;

public sealed class ChatRefactorTests
{
    [Test]
    public async Task Controller_GetById_Should_Return_Unauthorized_When_UserId_Claim_Is_MissingAsync()
    {
        var service = new Mock<IChatService>(MockBehavior.Strict);
        var controller = new ChatController(
            service.Object,
            Mock.Of<IChatRealtimeNotifier>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetByIdAsync(7);

        Assert.That(result, Is.TypeOf<UnauthorizedResult>());
    }

    [Test]
    public async Task Controller_GetById_Should_Return_Forbid_When_Service_Returns_ForbiddenAsync()
    {
        var service = new Mock<IChatService>();
        service.Setup(x => x.GetByIdAsync(12, 7))
            .ReturnsAsync(ServiceResult<ChatDto>.Forbidden());

        var controller = new ChatController(
            service.Object,
            Mock.Of<IChatRealtimeNotifier>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("userId", "12") },
                        "test"))
                }
            }
        };

        var result = await controller.GetByIdAsync(7);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Service_GetById_Should_Return_Forbidden_When_Requester_Is_Not_Chat_MemberAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var member = await CreateUserAsync(serverDb, "member@example.test");
        var outsider = await CreateUserAsync(serverDb, "outsider@example.test");
        var chat = await CreateChatAsync(serverDb, ChatType.Group, "general");
        await AddMemberAsync(serverDb, chat.Id, member.Id, ChatMemberRole.Base);

        var service = CreateService(serverDb);
        var result = await service.GetByIdAsync(outsider.Id, chat.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    private static ChatService CreateService(ServerDbContext serverDb)
    {
        return new ChatService(
            serverDb,
            Mock.Of<IChatMemberService>(),
            NullLogger<ChatService>.Instance,
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

    private static async Task<Chat> CreateChatAsync(ServerDbContext serverDb, ChatType type, string name)
    {
        var chat = new Chat
        {
            Name = name,
            Type = type,
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