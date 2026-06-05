#nullable enable

using Edemly.Client.Api;
using Edemly.Client.Infrastructure.Caching;
using System.Reflection;

namespace Edemly.Client.Application.Chats
{
    public class ChatLoader
    {
        private readonly ChatCache _cache;
        private readonly IApiService _apiService;
        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/Avatars/default-avatar.png";

        public ChatLoader(IApiService apiService, ChatCache cache)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<UserDto?> GetUserWithCacheAsync(int userId)
        {
            if (_cache.TryGetUser(userId, out var cachedUser))
            {
                return cachedUser;
            }

            var user = await _apiService.GetUserByIdAsync(userId);
            if (user != null)
            {
                _cache.AddUser(userId, user);
            }

            return user;
        }

        private static string GetDisplayNameFromUser(UserDto user)
        {
            if (user == null) return string.Empty;

            try
            {
                var t = user.GetType();
                var fnProp = t.GetProperty("FirstName", BindingFlags.Public | BindingFlags.Instance);
                var lnProp = t.GetProperty("LastName", BindingFlags.Public | BindingFlags.Instance);

                var first = fnProp != null ? fnProp.GetValue(user) as string : null;
                var last = lnProp != null ? lnProp.GetValue(user) as string : null;

                var full = $"{first?.Trim()} {last?.Trim()}".Trim();
                if (!string.IsNullOrEmpty(full))
                    return full;
            }
            catch { }

            return string.IsNullOrEmpty(user.Username) ? string.Empty : user.Username;
        }

        public async Task<List<MessageDto>> LoadChatMessagesAsync(int chatId, int page = 1, int pageSize = 50)
        {
            try
            {
                if (page == 1 && _cache.TryGetMessages(chatId, out var cachedMessages))
                {
                    return cachedMessages;
                }

                var messages = await _apiService.GetChatMessagesAsync(chatId, page, pageSize);

                if (messages.Count > 0)
                {
                    messages = messages.OrderBy(m => m.SentAt).ToList();

                    if (page == 1)
                    {
                        _cache.AddMessages(chatId, messages);
                    }

                    return messages;
                }

                return new List<MessageDto>();
            }
            catch (Exception)
            {
                return new List<MessageDto>();
            }
        }

        public async Task<(Models.Contact contact, int chatId)?> LoadSingleChatAsync(ChatDto chat, int currentUserId)
        {
            try
            {
                if (chat.Type == 0)
                {
                    var members = await _apiService.GetChatMembersAsync(chat.Id);

                    if (members.Count > 0)
                    {
                        var otherMember = members.FirstOrDefault(m => m.UserId != currentUserId);

                        if (otherMember == null)
                        {
                            return null;
                        }

                        var user = await GetUserWithCacheAsync(otherMember.UserId);
                        if (user != null)
                        {
                            var photoPath = string.IsNullOrEmpty(user.PfpUrl) ? DEFAULT_AVATAR_PATH : user.PfpUrl;

                            var displayName = GetDisplayNameFromUser(user);

                            var contact = new Models.Contact(
                                user.Id,
                                displayName,
                                $"{user.Username}@user.com",
                                "",
                                photoPath
                            );

                            return (contact, chat.Id);
                        }
                    }
                }
                else
                {
                    var photoPath = string.IsNullOrEmpty(chat.IconUrl) ? DEFAULT_AVATAR_PATH : chat.IconUrl;
                    var contact = new Models.Contact(
                        chat.Id,
                        chat.Name,
                        "",
                        "",
                        photoPath
                    );

                    return (contact, chat.Id);
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public async Task<Dictionary<int, List<ChatMemberDto>>> LoadChatMembersBatchAsync(List<int> chatIds)
        {
            var result = new Dictionary<int, List<ChatMemberDto>>();

            var tasks = chatIds.Select(chatId => GetMembersForChatAsync(chatId)).ToArray();

            var results = await Task.WhenAll(tasks);

            foreach (var tup in results)
            {
                result[tup.Item1] = tup.Item2;
            }

            return result;

            async Task<(int, List<ChatMemberDto>)> GetMembersForChatAsync(int chatId)
            {
                try
                {
                    var members = await _apiService.GetChatMembersAsync(chatId);
                    return (chatId, members);
                }
                catch (Exception)
                {
                    return (chatId, new List<ChatMemberDto>());
                }
            }
        }

        public async Task<Dictionary<int, UserDto>> LoadUsersBatchAsync(List<int> userIds)
        {
            var result = new Dictionary<int, UserDto>();
            var uniqueUserIds = userIds.Distinct().ToList();

            var uncachedUserIds = new List<int>();
            foreach (var userId in uniqueUserIds)
            {
                if (_cache.TryGetUser(userId, out var cachedUser))
                {
                    result[userId] = cachedUser;
                }
                else
                {
                    uncachedUserIds.Add(userId);
                }
            }

            if (uncachedUserIds.Count > 0)
            {
                var tasks = uncachedUserIds.Select(userId => GetUserAsync(userId)).ToArray();

                var results = await Task.WhenAll(tasks);

                var newUsers = results.Where(r => r.Item2 != null).Select(r => r.Item2!).ToList();
                if (newUsers.Count > 0)
                {
                    _cache.AddUsersBatch(newUsers);
                }

                foreach (var tup in results)
                {
                    var id = tup.Item1;
                    var user = tup.Item2;
                    if (user != null)
                    {
                        result[id] = user;
                    }
                }
            }

            return result;

            async Task<(int, UserDto?)> GetUserAsync(int userId)
            {
                try
                {
                    var user = await _apiService.GetUserByIdAsync(userId);
                    return (userId, user);
                }
                catch (Exception)
                {
                    return (userId, null);
                }
            }
        }
    }

}
