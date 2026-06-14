using Edemly.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Application.Messages;

internal static class ChatLastMessageSnapshot
{
    public static void Apply(Chat chat, Message message)
    {
        chat.LastMessageId = message.Id;
        chat.LastMessageText = message.Text;
        chat.LastMessageSenderId = message.SenderId;
        chat.LastMessageTime = message.SentAt;
    }

    public static void Clear(Chat chat)
    {
        chat.LastMessageId = null;
        chat.LastMessageText = null;
        chat.LastMessageSenderId = null;
        chat.LastMessageTime = null;
    }

    public static async Task ApplyAsync(DbContext ctx, Message message, CancellationToken cancellationToken = default)
    {
        var chat = await ctx.Set<Chat>()
            .FirstOrDefaultAsync(chat => chat.Id == message.ChatId, cancellationToken);

        if (chat != null)
        {
            Apply(chat, message);
        }
    }

    public static async Task ApplyIfCurrentAsync(DbContext ctx, Message message, CancellationToken cancellationToken = default)
    {
        var chat = await ctx.Set<Chat>()
            .FirstOrDefaultAsync(chat => chat.Id == message.ChatId, cancellationToken);

        if (chat != null && ReferencesMessage(chat, message))
        {
            Apply(chat, message);
        }
    }

    public static async Task RefreshAfterDeletingAsync(DbContext ctx, Message deletedMessage, CancellationToken cancellationToken = default)
    {
        var chat = await ctx.Set<Chat>()
            .FirstOrDefaultAsync(chat => chat.Id == deletedMessage.ChatId, cancellationToken);

        if (chat == null || !ReferencesMessage(chat, deletedMessage))
        {
            return;
        }

        var previousMessage = await ctx.Set<Message>()
            .AsNoTracking()
            .Where(message => message.ChatId == deletedMessage.ChatId && message.Id != deletedMessage.Id)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousMessage == null)
        {
            Clear(chat);
            return;
        }

        Apply(chat, previousMessage);
    }

    private static bool ReferencesMessage(Chat chat, Message message)
    {
        if (chat.LastMessageId == message.Id)
        {
            return true;
        }

        return chat.LastMessageId == null
            && chat.LastMessageTime == message.SentAt
            && chat.LastMessageSenderId == message.SenderId;
    }
}
