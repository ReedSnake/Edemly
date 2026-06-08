#nullable disable
using Edemly.Contracts.Chats;
using Edemly.Contracts.Messages;
using Edemly.Contracts.Users;

namespace Edemly.Client.Infrastructure.Caching
{
    public class ChatCache
    {
        private readonly Dictionary<int, CachedChatDto> _chatsCache = new();
        private readonly Dictionary<int, CachedMessagesDto> _messagesCache = new();
        private readonly Dictionary<int, CachedUserDto> _usersCache = new();
        private readonly ReaderWriterLockSlim _chatLock = new();
        private readonly ReaderWriterLockSlim _messageLock = new();
        private readonly ReaderWriterLockSlim _userLock = new();

        private readonly TimeSpan _chatCacheExpiration = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _messageCacheExpiration = TimeSpan.FromMinutes(10);
        private readonly TimeSpan _userCacheExpiration = TimeSpan.FromMinutes(15);

        #region Chat Cache

        public bool TryGetChat(int chatId, out ChatDto chat)
        {
            _chatLock.EnterReadLock();
            try
            {
                if (_chatsCache.TryGetValue(chatId, out var cached))
                {
                    if (DateTime.UtcNow - cached.CachedAt < _chatCacheExpiration)
                    {
                        chat = cached.Chat;
                        return true;
                    }
                }

                chat = null;
                return false;
            }
            finally
            {
                _chatLock.ExitReadLock();
            }
        }

        public void AddChat(int chatId, ChatDto chat)
        {
            _chatLock.EnterWriteLock();
            try
            {
                _chatsCache[chatId] = new CachedChatDto
                {
                    Chat = chat,
                    CachedAt = DateTime.UtcNow
                };
            }
            finally
            {
                _chatLock.ExitWriteLock();
            }
        }

        public void AddChatsBatch(IEnumerable<ChatDto> chats)
        {
            _chatLock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                foreach (var chat in chats)
                {
                    _chatsCache[chat.Id] = new CachedChatDto
                    {
                        Chat = chat,
                        CachedAt = now
                    };
                }
            }
            finally
            {
                _chatLock.ExitWriteLock();
            }
        }

        #endregion Chat Cache

        #region Messages Cache

        public bool TryGetMessages(int chatId, out List<MessageDto> messages)
        {
            _messageLock.EnterReadLock();
            try
            {
                if (_messagesCache.TryGetValue(chatId, out var cached))
                {
                    if (DateTime.UtcNow - cached.CachedAt < _messageCacheExpiration)
                    {
                        messages = cached.Messages;
                        return true;
                    }
                }

                messages = null;
                return false;
            }
            finally
            {
                _messageLock.ExitReadLock();
            }
        }

        public void AddMessages(int chatId, List<MessageDto> messages)
        {
            _messageLock.EnterWriteLock();
            try
            {
                _messagesCache[chatId] = new CachedMessagesDto
                {
                    Messages = messages.OrderBy(m => m.SentAt).ToList(),
                    CachedAt = DateTime.UtcNow
                };
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        public void AddMessage(int chatId, MessageDto message)
        {
            _messageLock.EnterWriteLock();
            try
            {
                if (!_messagesCache.ContainsKey(chatId))
                {
                    _messagesCache[chatId] = new CachedMessagesDto
                    {
                        Messages = new List<MessageDto>(),
                        CachedAt = DateTime.UtcNow
                    };
                }

                _messagesCache[chatId].Messages.Add(message);
                _messagesCache[chatId].CachedAt = DateTime.UtcNow;
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        public void UpdateMessage(int chatId, MessageDto updatedMessage)
        {
            _messageLock.EnterWriteLock();
            try
            {
                if (_messagesCache.TryGetValue(chatId, out var cached))
                {
                    var index = cached.Messages.FindIndex(m => m.Id == updatedMessage.Id);
                    if (index >= 0)
                    {
                        cached.Messages[index] = updatedMessage;
                        cached.CachedAt = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        public void RemoveMessage(int chatId, int messageId)
        {
            _messageLock.EnterWriteLock();
            try
            {
                if (_messagesCache.TryGetValue(chatId, out var cached))
                {
                    cached.Messages.RemoveAll(m => m.Id == messageId);
                    cached.CachedAt = DateTime.UtcNow;
                }
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        public void InvalidateMessages(int chatId)
        {
            _messageLock.EnterWriteLock();
            try
            {
                _messagesCache.Remove(chatId);
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        #endregion Messages Cache

        #region User Cache

        public bool TryGetUser(int userId, out UserDto user)
        {
            _userLock.EnterReadLock();
            try
            {
                if (_usersCache.TryGetValue(userId, out var cached))
                {
                    if (DateTime.UtcNow - cached.CachedAt < _userCacheExpiration)
                    {
                        user = cached.User;
                        return true;
                    }
                }

                user = null;
                return false;
            }
            finally
            {
                _userLock.ExitReadLock();
            }
        }

        public void AddUser(int userId, UserDto user)
        {
            _userLock.EnterWriteLock();
            try
            {
                _usersCache[userId] = new CachedUserDto
                {
                    User = user,
                    CachedAt = DateTime.UtcNow
                };
            }
            finally
            {
                _userLock.ExitWriteLock();
            }
        }

        public void AddUsersBatch(IEnumerable<UserDto> users)
        {
            _userLock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                foreach (var user in users)
                {
                    _usersCache[user.Id] = new CachedUserDto
                    {
                        User = user,
                        CachedAt = now
                    };
                }
            }
            finally
            {
                _userLock.ExitWriteLock();
            }
        }

        #endregion User Cache

        #region Cache Management

        public void CleanupExpiredEntries()
        {
            CleanupChats();
            CleanupMessages();
            CleanupUsers();
        }

        private void CleanupChats()
        {
            _chatLock.EnterWriteLock();
            try
            {
                var expired = _chatsCache
                    .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt >= _chatCacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    _chatsCache.Remove(key);
                }
            }
            finally
            {
                _chatLock.ExitWriteLock();
            }
        }

        private void CleanupMessages()
        {
            _messageLock.EnterWriteLock();
            try
            {
                var expired = _messagesCache
                    .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt >= _messageCacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    _messagesCache.Remove(key);
                }
            }
            finally
            {
                _messageLock.ExitWriteLock();
            }
        }

        private void CleanupUsers()
        {
            _userLock.EnterWriteLock();
            try
            {
                var expired = _usersCache
                    .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt >= _userCacheExpiration)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    _usersCache.Remove(key);
                }
            }
            finally
            {
                _userLock.ExitWriteLock();
            }
        }

        public void ClearAll()
        {
            _chatLock.EnterWriteLock();
            _messageLock.EnterWriteLock();
            _userLock.EnterWriteLock();
            try
            {
                _chatsCache.Clear();
                _messagesCache.Clear();
                _usersCache.Clear();
            }
            finally
            {
                _userLock.ExitWriteLock();
                _messageLock.ExitWriteLock();
                _chatLock.ExitWriteLock();
            }
        }

        public int GetCacheSize()
        {
            _chatLock.EnterReadLock();
            _messageLock.EnterReadLock();
            _userLock.EnterReadLock();
            try
            {
                var messageCount = _messagesCache.Sum(kvp => kvp.Value.Messages.Count);
                return _chatsCache.Count + messageCount + _usersCache.Count;
            }
            finally
            {
                _userLock.ExitReadLock();
                _messageLock.ExitReadLock();
                _chatLock.ExitReadLock();
            }
        }

        #endregion Cache Management

        public void Dispose()
        {
            _chatLock?.Dispose();
            _messageLock?.Dispose();
            _userLock?.Dispose();
        }
    }
}