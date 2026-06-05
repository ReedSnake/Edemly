#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        private async Task PresenceTimerTickAsync()
        {
            try
            {
                if (_hubService == null) return;

                await RefreshAllPrivateChatStatusesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] PresenceTimerTickAsync error: {ex.Message}");
            }
        }

        private bool TryGetCachedStatus(int userId, out bool isOnline, out DateTime? lastSeenUtc)
        {
            isOnline = false;
            lastSeenUtc = null;
            try
            {
                lock (_statusCacheLock)
                {
                    if (_userStatusCache.TryGetValue(userId, out var entry))
                    {
                        if (DateTime.UtcNow <= entry.ExpiresAtUtc)
                        {
                            isOnline = entry.IsOnline;
                            lastSeenUtc = entry.LastSeenUtc;
                            return true;
                        }

                        _userStatusCache.Remove(userId);
                        return false;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] TryGetCachedStatus failed: {ex}"); }
            return false;
        }

        private void UpdateStatusCache(int userId, bool isOnline, DateTime? lastSeenUtc)
        {
            try
            {
                var expires = DateTime.UtcNow.Add(_statusTtl);
                lock (_statusCacheLock)
                {
                    _userStatusCache[userId] = (isOnline, lastSeenUtc, expires);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateStatusCache failed: {ex}"); }
        }

        public bool TryGetCachedUserStatus(int userId, out bool isOnline, out DateTime? lastSeenUtc)
        {
            return TryGetCachedStatus(userId, out isOnline, out lastSeenUtc);
        }

        public async Task<(bool Found, bool IsOnline, DateTime? LastSeenUtc)> RefreshUserStatusAsync(int userId)
        {
            try
            {
                var statusObj = await _hubService.QueryUserStatusAsync(userId);
                if (statusObj == null)
                {
                    return (false, false, null);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(statusObj);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var status = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);
                if (status == null)
                {
                    return (false, false, null);
                }

                UpdateStatusCache(userId, status.IsOnline, status.LastSeen);

                var chatIds = _chatToUserMap
                    .Where(kv => kv.Value == userId)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var chatId in chatIds)
                {
                    UpdateChatButtonOnline(chatId, status.IsOnline);
                }

                return (true, status.IsOnline, status.LastSeen);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] RefreshUserStatusAsync failed for user {userId}: {ex.Message}");
                return (false, false, null);
            }
        }

        private bool GetCachedOnlineForChat(int chatId)
        {
            if (_chatToUserMap.TryGetValue(chatId, out var userId) &&
                userId > 0 &&
                TryGetCachedStatus(userId, out var isOnline, out _))
            {
                return isOnline;
            }

            return false;
        }
    }
}
