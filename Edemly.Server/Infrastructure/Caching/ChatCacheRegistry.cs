using Microsoft.Extensions.Caching.Memory;

namespace Edemly.Server.Utils
{
    public class ChatCacheRegistry //I use this so cache gets cleared when messages in a specific chat get changed in any way
    {
        private readonly Dictionary<int, Dictionary<(int page, int size), string>> _registry = new();

        public static string GetCacheKey(int chatId, int page, int pageSize) => $"chat:{chatId}:messages:page:{page}:size:{pageSize}";

        public static string GetLastMessageCacheKey(int chatId) => $"chat:{chatId}:last-message";

        public void RegisterKey(int chatId, int page, int pageSize)
        {
            if (!_registry.ContainsKey(chatId))
                _registry[chatId] = new Dictionary<(int, int), string>();

            _registry[chatId][(page, pageSize)] = ChatCacheRegistry.GetCacheKey(chatId, page, pageSize);
        }

        public IEnumerable<string> GetKeys(int chatId)
        {
            if (_registry.TryGetValue(chatId, out var pages))
                return pages.Values;

            return Enumerable.Empty<string>();
        }

        public void ClearChat(int chatId, IMemoryCache cache)
        {
            cache.Remove(GetLastMessageCacheKey(chatId));

            if (_registry.TryGetValue(chatId, out var pages))
            {
                foreach (var key in pages.Values)
                    cache.Remove(key);

                pages.Clear();
            }
        }
    }
}