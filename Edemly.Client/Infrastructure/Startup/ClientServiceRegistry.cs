using Edemly.Client.Api;
using Edemly.Client.Application.Services;
using Edemly.Client.Infrastructure.Caching;
using Edemly.Client.Infrastructure.Realtime;
using System.Diagnostics;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class ClientServiceRegistry : IDisposable
    {
        private readonly Func<Task<string?>> _authTokenProvider;

        public ClientServiceRegistry(Func<Task<string?>> authTokenProvider)
        {
            _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
        }

        public IApiService ApiService { get; private set; } = null!;
        public IAuthService AuthService { get; private set; } = null!;
        public IHubService HubService { get; private set; } = null!;
        public NotesService? NotesService { get; private set; }

        public ChatCache ChatCache { get; } = new ChatCache();
        public ProfilePictureCache ProfilePictureCache { get; private set; } = null!;
        public FileCache FileCache { get; private set; } = null!;

        public void Initialize(string apiBase, string cacheScope)
        {
            ApiService = new ApiService(apiBase);
            AuthService = new AuthService(apiBase);
            HubService = new HubService(apiBase);
            ProfilePictureCache = new ProfilePictureCache(apiBase, _authTokenProvider, cacheScope);
            FileCache = new FileCache(apiBase, _authTokenProvider, cacheScope);

            try
            {
                NotesService = new NotesService(ApiService);
            }
            catch (Exception ex)
            {
                NotesService = null;
                Debug.WriteLine($"[SERVICE REGISTRY] Failed to initialize NotesService: {ex.Message}");
            }
        }

        public void SetAuthToken(string? token)
        {
            try
            {
                ApiService?.SetAuthToken(token ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] ApiService.SetAuthToken failed: {ex}");
            }

            try
            {
                ProfilePictureCache?.SetAuthToken(token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] ProfilePictureCache.SetAuthToken failed: {ex}");
            }
        }

        public void ClearConversationState()
        {
            ChatCache.ClearAll();

            try
            {
                NotesService?.ClearCache();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] NotesService.ClearCache failed: {ex}");
            }
        }

        public void ClearMediaCaches()
        {
            try
            {
                ProfilePictureCache?.ClearAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] ProfilePictureCache.ClearAll failed: {ex}");
            }

            try
            {
                FileCache?.ClearAll();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] FileCache.ClearAll failed: {ex}");
            }
        }

        public void DisposeCoreServices()
        {
            try
            {
                (HubService as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] Dispose HubService failed: {ex}");
            }

            try
            {
                (ApiService as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] Dispose ApiService failed: {ex}");
            }
        }

        public void DisposeMediaCaches()
        {
            try
            {
                ProfilePictureCache?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] Dispose ProfilePictureCache failed: {ex}");
            }

            try
            {
                FileCache?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] Dispose FileCache failed: {ex}");
            }
        }

        public void Dispose()
        {
            DisposeCoreServices();

            try
            {
                ChatCache.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE REGISTRY] Dispose ChatCache failed: {ex}");
            }

            DisposeMediaCaches();
        }
    }
}
