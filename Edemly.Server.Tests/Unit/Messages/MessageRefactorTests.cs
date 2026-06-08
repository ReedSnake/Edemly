using Edemly.Contracts.Messages;
using Edemly.Server.Api.Controllers.Messages;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Messages;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Caching;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Edemly.Server.Tests.Unit.Messages;

public sealed class MessageRefactorTests
{
    [Test]
    public async Task Controller_GetByChat_Should_Return_Forbid_When_Service_Returns_ForbiddenAsync()
    {
        var service = new Mock<IMessageService>();
        service.Setup(x => x.GetByChatAsync(12, 7, 1, 20))
            .ReturnsAsync(ServiceResult<List<MessageDto>>.Forbidden());

        var controller = new MessageController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(12)
                }
            }
        };

        var result = await controller.GetByChatAsync(7);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Service_Create_Should_Return_Forbidden_When_User_Is_Not_In_ChatAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var user = await CreateUserAsync(serverDb, "user@example.test");
        var chat = await CreateChatAsync(serverDb);

        var service = CreateService(serverDb);
        var result = await service.CreateAsync(user.Id, new CreateMessageDto
        {
            ChatId = chat.Id,
            Text = "hi",
            Type = (int)MessageType.Txt
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Service_Create_Should_Return_NotFound_When_Chat_Does_Not_ExistAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var user = await CreateUserAsync(serverDb, "user@example.test");
        var service = CreateService(serverDb);
        var result = await service.CreateAsync(user.Id, new CreateMessageDto
        {
            ChatId = 999,
            Text = "hi",
            Type = (int)MessageType.Txt
        });

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task Service_Delete_Should_Allow_Creator_To_Delete_Chat_MessageAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var creator = await CreateUserAsync(serverDb, "creator@example.test");
        var sender = await CreateUserAsync(serverDb, "sender@example.test");
        var chat = await CreateChatAsync(serverDb);
        await AddMemberAsync(serverDb, chat.Id, creator.Id, ChatMemberRole.Creator);
        await AddMemberAsync(serverDb, chat.Id, sender.Id, ChatMemberRole.Base);

        var message = new Message
        {
            ChatId = chat.Id,
            SenderId = sender.Id,
            Text = "delete me",
            Type = MessageType.Txt,
            SentAt = DateTime.UtcNow
        };
        serverDb.Messages.Add(message);
        await serverDb.SaveChangesAsync();

        var service = CreateService(serverDb);
        var result = await service.DeleteAsync(creator.Id, message.Id);

        Assert.That(result.Success, Is.True);
        Assert.That(serverDb.Messages.Any(), Is.False);
    }

    private static MessageService CreateService(ServerDbContext serverDb)
    {
        return new MessageService(
            serverDb,
            NullLogger<MessageService>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            new ChatCacheRegistry(),
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