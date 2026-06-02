using System.Collections.Concurrent;
using Edemly.Server.Models;

namespace Edemly.Server.Services
{
    /// <summary>
    /// Сервіс для управління онлайн-статусом користувачів
    /// </summary>
    public class UserPresenceService
    {
        private readonly ConcurrentDictionary<int, UserOnlineStatus> _userStatuses = new();
        private readonly ConcurrentDictionary<string, int> _connectionToUser = new();
        // Мапа userId -> set of connectionIds
        private readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = new();

        public void SetUserOnline(int userId, string connectionId)
        {
            _userConnections.AddOrUpdate(userId,
                new HashSet<string> { connectionId },
                (key, existing) =>
                {
                    existing.Add(connectionId);
                    return existing;
                });

            _userStatuses.AddOrUpdate(userId,
                new UserOnlineStatus
                {
                    UserId = userId,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow,
                    ConnectionId = connectionId
                },
                (key, existing) =>
                {
                    existing.IsOnline = true;
                    existing.LastSeen = DateTime.UtcNow;
                    existing.ConnectionId = connectionId;
                    return existing;
                });

            _connectionToUser.TryAdd(connectionId, userId);
        }

        /// <summary>
        /// Удалить соединение. Возвращает кортеж: (isStillOnline, userIdIfKnown)
        /// </summary>
        public (bool StillOnline, int? UserId) SetUserOffline(string connectionId)
        {
            if (_connectionToUser.TryRemove(connectionId, out var userId))
            {
                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);

                    if (connections.Count > 0)
                    {
                        // Пользователь все еще имеет другие активные соединения
                        return (true, userId);
                    }

                    // Удаляем запись о соединениях
                    _userConnections.TryRemove(userId, out _);
                }

                if (_userStatuses.TryGetValue(userId, out var status))
                {
                    status.IsOnline = false;
                    status.LastSeen = DateTime.UtcNow;
                    status.ConnectionId = null;
                }

                return (false, userId);
            }

            return (false, null);
        }

        public UserOnlineStatus? GetUserStatus(int userId)
        {
            return _userStatuses.TryGetValue(userId, out var status) ? status : null;
        }

        public List<UserOnlineStatus> GetOnlineUsers()
        {
            return _userStatuses.Values.Where(s => s.IsOnline).ToList();
        }
    }
}
