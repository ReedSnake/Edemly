using Edemly.Client.Infrastructure.Startup;
using System.Diagnostics;

namespace Edemly.Client.Application.Session
{
    public sealed class ClientSessionManager
    {
        private readonly ClientUserSession _session;
        private readonly ClientServiceRegistry _serviceRegistry;
        private readonly Action _disposeChatController;
        private readonly Action _hideStatusBar;
        private readonly Action _clearChatActivationCache;

        public ClientSessionManager(
            ClientUserSession session,
            ClientServiceRegistry serviceRegistry,
            Action disposeChatController,
            Action hideStatusBar,
            Action clearChatActivationCache)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException(nameof(serviceRegistry));
            _disposeChatController = disposeChatController ?? throw new ArgumentNullException(nameof(disposeChatController));
            _hideStatusBar = hideStatusBar ?? throw new ArgumentNullException(nameof(hideStatusBar));
            _clearChatActivationCache = clearChatActivationCache ?? throw new ArgumentNullException(nameof(clearChatActivationCache));
        }

        public void SetCurrentUser(int userId, string email, string userName, string? photoUrl = null, string? token = null)
        {
            _session.SetCurrentUser(userId, email, userName, photoUrl, token);

            if (!string.IsNullOrEmpty(token))
            {
                _serviceRegistry.SetAuthToken(token);
            }

            Debug.WriteLine($"[SESSION] User set: id={userId}, email={email}, name={userName}");
        }

        public async Task RefreshCurrentUserProfileAsync()
        {
            try
            {
                if (_serviceRegistry.ApiService == null || !_session.UserId.HasValue)
                {
                    return;
                }

                var userInfo = await _serviceRegistry.ApiService.GetUserInfoAsync();
                if (userInfo == null || userInfo.Id <= 0)
                {
                    return;
                }

                _session.PhotoUrl = userInfo.PfpUrl;
                CurrentUserProfileState.UserName = userInfo.Username ?? string.Empty;
                CurrentUserProfileState.Email = userInfo.Email ?? string.Empty;
                CurrentUserProfileState.PhoneNumber = userInfo.PhoneNumber ?? string.Empty;
                CurrentUserProfileState.PfpUrl = userInfo.PfpUrl ?? string.Empty;
                CurrentUserProfileState.Description = userInfo.Description ?? string.Empty;
                CurrentUserProfileState.FirstName = userInfo.FirstName ?? string.Empty;
                CurrentUserProfileState.LastName = userInfo.LastName ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(userInfo.PfpUrl))
                {
                    try
                    {
                        await _serviceRegistry.ProfilePictureCache.GetOrDownloadAsync(userInfo.PfpUrl);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SESSION] Preload profile picture failed: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] RefreshCurrentUserProfileAsync failed: {ex.Message}");
            }
        }

        public void ClearCurrentUser()
        {
            _session.Clear();
            _serviceRegistry.SetAuthToken(null);

            try
            {
                _disposeChatController();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] Dispose chat controller failed: {ex}");
            }

            try
            {
                _hideStatusBar();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] Hide status bar failed: {ex}");
            }

            _serviceRegistry.ClearConversationState();
            _serviceRegistry.ClearMediaCaches();

            try
            {
                _clearChatActivationCache();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] Clear chat activation cache failed: {ex}");
            }

            CurrentUserProfileState.Clear();
            Debug.WriteLine("[SESSION] Cleared current user state");
        }
    }
}
