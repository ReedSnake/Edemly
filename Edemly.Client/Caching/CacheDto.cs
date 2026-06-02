using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edemly.Client.Caching
{
    public class CachedChatDto
    {
        public ChatDto Chat { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class CachedMessagesDto
    {
        public List<MessageDto> Messages { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class CachedUserDto
    {
        public UserDto User { get; set; }
        public DateTime CachedAt { get; set; }
    }
}
