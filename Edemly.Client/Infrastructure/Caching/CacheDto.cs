using Edemly.Contracts.Chats;
using Edemly.Contracts.Messages;
using Edemly.Contracts.Users;

namespace Edemly.Client.Infrastructure.Caching
{
    public class CachedChatDto
    {
        public ChatDto Chat { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }

    public class CachedMessagesDto
    {
        public List<MessageDto> Messages { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }

    public class CachedUserDto
    {
        public UserDto User { get; set; } = new();
        public DateTime CachedAt { get; set; }
    }
}