using Edemly.Contracts.ChatMembers;
using Edemly.Contracts.Chats;
using Edemly.Contracts.Messages;
using Edemly.Server.Data.Entities;
using System.Linq.Expressions;

namespace Edemly.Server.Application.Common.Mappers
{
    public static class ChatMappings
    {
        public static ChatDto ToDto(Chat chat, string? displayName = null, Message? lastMessage = null)
        {
            return new ChatDto
            {
                Id = chat.Id,
                Name = displayName ?? chat.Name,
                Description = chat.Description ?? string.Empty,
                IconUrl = chat.IconUrl ?? string.Empty,
                Type = (int)chat.Type,
                CreatedAt = chat.CreatedAt,
                LastMessageTime = chat.LastMessageTime,
                LastMessageText = lastMessage?.Text,
                LastMessageSenderId = lastMessage?.SenderId
            };
        }
    }

    public static class ChatMemberMappings
    {
        public static readonly Expression<Func<ChatMember, ChatMemberDto>> Projection = member => new ChatMemberDto
        {
            Id = member.Id,
            UserId = member.UserId,
            ChatId = member.ChatId,
            Role = (int)member.Role,
            JoinedAt = member.JoinedAt
        };

        public static ChatMemberDto ToDto(ChatMember member)
        {
            return new ChatMemberDto
            {
                Id = member.Id,
                UserId = member.UserId,
                ChatId = member.ChatId,
                Role = (int)member.Role,
                JoinedAt = member.JoinedAt
            };
        }
    }

    public static class MessageMappings
    {
        public static readonly Expression<Func<Message, MessageDto>> Projection = message => new MessageDto
        {
            Id = message.Id,
            ChatId = message.ChatId,
            SenderId = message.SenderId,
            Text = message.Text,
            Type = (int)message.Type,
            SentAt = message.SentAt,
            ContentUrl = message.ContentUrl,
            FileName = message.FileName
        };

        public static MessageDto ToDto(Message message)
        {
            return new MessageDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Text = message.Text,
                Type = (int)message.Type,
                SentAt = message.SentAt,
                ContentUrl = message.ContentUrl,
                FileName = message.FileName
            };
        }
    }
}