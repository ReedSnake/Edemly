#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using uchat;
using uchat.Models;
using uchat.DTOs;
using System.Reflection;
using uchat.Services.Api;

namespace uchat.Helpers
{
    public class ChatLoader
    {
        private readonly ChatCache _cache;
        private readonly IApiService _apiService;
        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/avatar.png";

        // Existing constructor (kept for compatibility)
        public ChatLoader(IApiService apiService, ChatCache cache)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _cache = cache;
        }

        // New constructor: use global App.ApiService so callers don't need to pass it
        public ChatLoader(ChatCache cache)
        {
            _apiService = App.ApiService ?? throw new InvalidOperationException("App.ApiService is not initialized");
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Завантажує користувача з кешем
        /// </summary>
        public async Task<UserDto?> GetUserWithCacheAsync(int userId)
        {
            // Спочатку перевіряємо кеш
            if (_cache.TryGetUser(userId, out var cachedUser))
            {
                return cachedUser;
            }

            // Якщо немає в кеші, завантажуємо з API
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
                // Try to read FirstName/LastName via reflection in case DTO was extended
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

            // Fallback to Username
            return string.IsNullOrEmpty(user.Username) ? string.Empty : user.Username;
        }

        /// <summary>
        /// Завантажує повідомлення чату з кешем
        /// </summary>
        public async Task<List<MessageDto>> LoadChatMessagesAsync(int chatId, int page = 1, int pageSize = 50)
        {
            try
            {
                // Якщо це перша сторінка, перевіряємо кеш
                if (page == 1 && _cache.TryGetMessages(chatId, out var cachedMessages))
                {
                    return cachedMessages;
                }

                // Завантажуємо з API
                var messages = await _apiService.GetChatMessagesAsync(chatId, page, pageSize);

                if (messages.Count > 0)
                {
                    messages = messages.OrderBy(m => m.SentAt).ToList();
                    
                    // Кешуємо тільки першу сторінку
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

        /// <summary>
        /// Завантажує один чат з оптимізацією
        /// </summary>
        public async Task<(Models.Contact contact, int chatId)?> LoadSingleChatAsync(ChatDto chat, int currentUserId)
        {
            try
            {
                // For private chats (Type = 0)
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
                    // For group chats
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

        /// <summary>
        /// Пакетне завантаження учасників для кількох чатів одночасно
        /// </summary>
        public async Task<Dictionary<int, List<ChatMemberDto>>> LoadChatMembersBatchAsync(List<int> chatIds)
        {
            var result = new Dictionary<int, List<ChatMemberDto>>();

            // Паралельно завантажуємо учасників для всіх чатів
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

        /// <summary>
        /// Пакетне завантаження користувачів з кешем
        /// </summary>
        public async Task<Dictionary<int, UserDto>> LoadUsersBatchAsync(List<int> userIds)
        {
            var result = new Dictionary<int, UserDto>();
            var uniqueUserIds = userIds.Distinct().ToList();

            // Спочатку перевіряємо кеш
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

            // Завантажуємо тільки тих користувачів, яких немає в кеші
            if (uncachedUserIds.Count > 0)
            {
                var tasks = uncachedUserIds.Select(userId => GetUserAsync(userId)).ToArray();

                var results = await Task.WhenAll(tasks);

                // Додаємо нових користувачів до кешу та результату
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

    public class ChatViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _photoPath = string.Empty;
        private int _chatId;
        private int _userId;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string PhotoPath
        {
            get => _photoPath;
            set
            {
                _photoPath = value;
                OnPropertyChanged();
            }
        }

        public int ChatId
        {
            get => _chatId;
            set
            {
                _chatId = value;
                OnPropertyChanged();
            }
        }

        public int UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}