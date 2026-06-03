using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.Chats;
using Edemly.Contracts.Messages;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Tests.Utilities;

public static class TestChatHelper
{
    public static async Task<int> CreatePrivateChatAsync(HttpClient client, int otherUserId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/chat/create-private",
            new CreatePrivateChatDto { UserId = otherUserId });

        await EnsureSuccessAsync(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("chat").GetProperty("id").GetInt32();
    }

    public static async Task<int> CreateGroupChatAsync(HttpClient client, string groupName, params int[] participantIds)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/chat/create-group",
            new CreateGroupChatDto
            {
                GroupName = groupName,
                ParticipantIds = participantIds.ToList()
            });

        await EnsureSuccessAsync(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("chat").GetProperty("id").GetInt32();
    }

    public static async Task SendTextMessageAsync(HttpClient client, int chatId, string text)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/message/create",
            new CreateMessageDto
            {
                ChatId = chatId,
                Text = text,
                Type = (int)MessageType.Txt
            });

        await EnsureSuccessAsync(response);
    }

    public static async Task<Message> GetMessageByTextAsync(IServiceProvider services, int chatId, string text)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.Messages
            .SingleAsync(message => message.ChatId == chatId && message.Text == text);
    }

    public static async Task<ChatMember> GetChatMemberAsync(IServiceProvider services, int chatId, int userId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.ChatMembers
            .SingleAsync(member => member.ChatId == chatId && member.UserId == userId);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Expected success status code but received {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}",
            inner: null,
            response.StatusCode);
    }
}
