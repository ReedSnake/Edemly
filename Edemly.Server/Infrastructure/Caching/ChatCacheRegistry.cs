using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Edemly.Server.Infrastructure.Caching
{
    public class ChatCacheRegistry
    {
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<(int page, int size), string>> _registry = new();

        public static string GetCacheKey(int chatId, int page, int pageSize) => $"chat:{chatId}:messages:page:{page}:size:{pageSize}";

        public static string GetLastMessageCacheKey(int chatId) => $"chat:{chatId}:last-message";

        public void RegisterKey(int chatId, int page, int pageSize)
        {
            var pages = _registry.GetOrAdd(
                chatId,
                _ => new ConcurrentDictionary<(int page, int size), string>());

            pages[(page, pageSize)] = GetCacheKey(chatId, page, pageSize);
        }

        public IEnumerable<string> GetKeys(int chatId)
        {
            if (_registry.TryGetValue(chatId, out var pages))
            {
                return pages.Values.ToArray();
            }

            return Enumerable.Empty<string>();
        }

        public void ClearChat(int chatId, IMemoryCache cache)
        {
            cache.Remove(GetLastMessageCacheKey(chatId));

            if (_registry.TryRemove(chatId, out var pages))
            {
                foreach (var key in pages.Values)
                {
                    cache.Remove(key);
                }
            }
        }
    }
}
