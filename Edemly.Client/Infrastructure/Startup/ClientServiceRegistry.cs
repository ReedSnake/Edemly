using Edemly.Client.Api;
using Edemly.Client.Api.Core;
using Edemly.Client.Api.Files;
using Edemly.Client.Application.Auth;
using Edemly.Client.Application.Notes;
using Edemly.Client.Infrastructure.Caching;
using Edemly.Client.Infrastructure.Realtime;
using System.Diagnostics;
using System.Net.Http;

namespace Edemly.Client.Infrastructure.Startup;

public sealed class ClientServiceRegistry : IDisposable
{
    private readonly Func<Task<string?>> _authTokenProvider;

    private HttpClient? _httpClient;
    private ApiClientContext? _apiContext;

    public ClientServiceRegistry(Func<Task<string?>> authTokenProvider)
    {
        _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
    }

    public IApiClients ApiClients { get; private set; } = null!;
    public IFileApiClient FileApiClient => ApiClients.Files;

    public IAuthService AuthService { get; private set; } = null!;
    public IHubService HubService { get; private set; } = null!;
    public NotesService? NotesService { get; private set; }

    public ChatCache ChatCache { get; } = new();
    public ProfilePictureCache ProfilePictureCache { get; private set; } = null!;
    public FileCache FileCache { get; private set; } = null!;

    public void Initialize(string apiBase, string hubBase, string cacheScope)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiBase)
        };

        _apiContext = new ApiClientContext(_httpClient);

        ApiClients = new ApiClients(_apiContext);

        AuthService = new AuthService(apiBase);
        HubService = new HubService(hubBase);

        ProfilePictureCache = new ProfilePictureCache(apiBase, _authTokenProvider, cacheScope);
        FileCache = new FileCache(apiBase, _authTokenProvider, cacheScope);

        try
        {
            NotesService = new NotesService(ApiClients.Notes);
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
            _apiContext?.SetAuthToken(token ?? string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVICE REGISTRY] ApiContext.SetAuthToken failed: {ex}");
        }

        try
        {
            ProfilePictureCache?.SetAuthToken(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVICE REGISTRY] ProfilePictureCache.SetAuthToken failed: {ex}");
        }

        try
        {
            FileCache?.SetAuthToken(token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVICE REGISTRY] FileCache.SetAuthToken failed: {ex}");
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
            _httpClient?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVICE REGISTRY] Dispose HttpClient failed: {ex}");
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

        try
        {
            ProfilePictureCache?.Dispose();
            FileCache?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SERVICE REGISTRY] Dispose caches failed: {ex}");
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
}
