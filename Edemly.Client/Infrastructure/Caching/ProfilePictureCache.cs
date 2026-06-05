#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
namespace Edemly.Client.Infrastructure.Caching
{
    public class ProfilePictureCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, BitmapImage> _memoryCache;
        private readonly string _diskCachePath;
        private readonly HttpClient _httpClient;
        private readonly string _serverBaseUrl; // normalized, ends with '/'
        private const int MAX_MEMORY_CACHE_SIZE = 50;
        private readonly object _cacheLock = new object();

        private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _downloadTasks = new();

        public event Action<string>? DownloadStarted;

        public event Action<string, BitmapImage>? DownloadCompleted;

        public event Action<string, Exception>? DownloadFailed;

        public string CacheScope { get; }

        private readonly Func<Task<string?>>? _tokenProvider;
        private string? _staticToken;

        public ProfilePictureCache(string serverBaseUrl, string cacheScope = "personal")
            : this(serverBaseUrl, null, cacheScope)
        {
        }

        public ProfilePictureCache(string serverBaseUrl, Func<Task<string?>>? tokenProvider, string cacheScope = "personal")
        {
            _memoryCache = new ConcurrentDictionary<string, BitmapImage>();

            CacheScope = MakeSafeFolderName(cacheScope);
            var safeScope = CacheScope;
            _diskCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Edemly",
                "cache",
                "profile_pictures",
                safeScope);

            Directory.CreateDirectory(_diskCachePath);

            _httpClient = new HttpClient();

            _serverBaseUrl = NormalizeBaseUrl(serverBaseUrl);

            _tokenProvider = tokenProvider;
        }

        public void SetAuthToken(string? bearerToken)
        {
            _staticToken = string.IsNullOrWhiteSpace(bearerToken) ? null : bearerToken;
        }

        private static string MakeSafeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "personal";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        private static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            if (!url.Contains("://"))
                url = "https://" + url;
            if (!url.EndsWith('/')) url += '/';
            return url;
        }

        public async Task<BitmapImage?> GetOrDownloadAsync(string pfpUrl)
        {
            if (string.IsNullOrEmpty(pfpUrl))
                return null;

            var cacheKey = GetCacheKey(pfpUrl);

            if (_memoryCache.TryGetValue(cacheKey, out var cachedImage))
            {
                return cachedImage;
            }

            var diskPath = FindDiskCachePath(cacheKey);
            if (diskPath != null && File.Exists(diskPath))
            {
                try
                {
                    var image = LoadImageFromDisk(diskPath);
                    if (image != null)
                    {
                        AddToMemoryCache(cacheKey, image);
                        return image;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProfilePictureCache] LoadImageFromDisk failed for {diskPath}: {ex.Message}");
                }
            }

            var downloadTask = _downloadTasks.GetOrAdd(cacheKey, _ => DownloadAndCacheAsync(pfpUrl, cacheKey));

            try
            {
                var result = await downloadTask;
                return result;
            }
            finally
            {
                _downloadTasks.TryRemove(cacheKey, out _);
            }
        }

        private async Task<BitmapImage?> DownloadAndCacheAsync(string pfpUrl, string cacheKey)
        {
            DownloadStarted?.Invoke(pfpUrl);

            try
            {
                var (data, contentType) = await DownloadImageWithRetriesAsync(pfpUrl, maxAttempts: 3);
                if (data == null || data.Length == 0)
                    return null;

                var ext = ExtensionFromContentType(contentType) ?? GetExtensionFromUrl(pfpUrl) ?? ".jpg";

                var diskPath = GetDiskCachePath(cacheKey, ext);

                await SaveToDiskAsync(diskPath, data);

                var image = LoadImageFromBytes(data);
                if (image != null)
                {
                    AddToMemoryCache(cacheKey, image);
                    DownloadCompleted?.Invoke(pfpUrl, image);
                    return image;
                }

                return null;
            }
            catch (Exception ex)
            {
                DownloadFailed?.Invoke(pfpUrl, ex);
                return null;
            }
        }

        public async Task<BitmapImage?> ForceDownloadAsync(string pfpUrl)
        {
            if (string.IsNullOrEmpty(pfpUrl))
                return null;

            var cacheKey = GetCacheKey(pfpUrl);

            try
            {
                var (data, contentType) = await DownloadImageWithRetriesAsync(pfpUrl, maxAttempts: 3);
                if (data == null || data.Length == 0) return null;

                var ext = ExtensionFromContentType(contentType) ?? GetExtensionFromUrl(pfpUrl) ?? ".jpg";

                var uniqueName = $"{cacheKey}_{DateTime.UtcNow.Ticks}{ext}";
                var diskPathNew = Path.Combine(_diskCachePath, uniqueName);

                await SaveToDiskAsync(diskPathNew, data);

                var image = LoadImageFromBytes(data);
                if (image != null)
                {
                    _memoryCache.TryRemove(cacheKey, out _);
                    AddToMemoryCache(cacheKey, image);

                    try
                    {
                        var all = GetAllDiskCachePaths(cacheKey);
                        foreach (var f in all)
                        {
                            try
                            {
                                if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(diskPathNew), StringComparison.OrdinalIgnoreCase))
                                    File.Delete(f);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ProfilePictureCache] Failed to delete old cache file '{f}': {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ProfilePictureCache] Cleaning old cache files failed: {ex.Message}");
                    }

                    return image;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] ForceDownloadAsync failed for '{pfpUrl}': {ex.Message}");
            }

            return null;
        }

        public async Task<BitmapImage?> CacheLocalFileAsync(string filePath)
        {
            try
            {
                var imageData = await File.ReadAllBytesAsync(filePath);
                var cacheKey = GetCacheKey(filePath);
                var ext = Path.GetExtension(filePath);
                var diskPath = GetDiskCachePath(cacheKey, ext);

                await SaveToDiskAsync(diskPath, imageData);

                var image = LoadImageFromBytes(imageData);
                if (image != null)
                {
                    AddToMemoryCache(cacheKey, image);
                }

                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] CacheLocalFileAsync failed: {ex.Message}");
                return null;
            }
        }

        public void InvalidateCache(string pfpUrl)
        {
            if (string.IsNullOrEmpty(pfpUrl))
                return;

            lock (_cacheLock)
            {
                var cacheKey = GetCacheKey(pfpUrl);

                _memoryCache.TryRemove(cacheKey, out _);

                var diskFiles = GetAllDiskCachePaths(cacheKey);
                foreach (var disk in diskFiles)
                {
                    if (disk != null && File.Exists(disk))
                    {
                        try { File.Delete(disk); } catch (Exception ex) { Debug.WriteLine($"[ProfilePictureCache] Failed to delete disk cache file '{disk}': {ex.Message}"); }
                    }
                }
            }
        }

        public void ClearAll()
        {
            lock (_cacheLock)
            {
                _memoryCache.Clear();

                try
                {
                    if (Directory.Exists(_diskCachePath))
                    {
                        Directory.Delete(_diskCachePath, true);
                        Directory.CreateDirectory(_diskCachePath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProfilePictureCache] ClearAll failed: {ex.Message}");
                }
            }
        }

        #region Private Methods

        private string GetCacheKey(string url)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private string GetDiskCachePath(string cacheKey, string extension)
        {
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";
            if (!extension.StartsWith('.')) extension = "." + extension;
            return Path.Combine(_diskCachePath, $"{cacheKey}{extension}");
        }

        private string[] GetAllDiskCachePaths(string cacheKey)
        {
            try
            {
                var dir = new DirectoryInfo(_diskCachePath);
                var files = dir.GetFiles(cacheKey + "*.*");
                return files.Select(f => f.FullName).ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] GetAllDiskCachePaths failed: {ex.Message}");
            }
            return Array.Empty<string>();
        }

        private string? FindDiskCachePath(string cacheKey)
        {
            try
            {
                var dir = new DirectoryInfo(_diskCachePath);
                var files = dir.GetFiles(cacheKey + "*.*");
                if (files.Length > 0)
                {
                    var newest = files.OrderByDescending(f => f.LastWriteTimeUtc).First();
                    return newest.FullName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] FindDiskCachePath failed: {ex.Message}");
            }
            return null;
        }

        private async Task<(byte[]? data, string? contentType)> DownloadImageWithRetriesAsync(string url, int maxAttempts = 3)
        {
            int attempt = 0;
            Exception? lastEx = null;
            while (attempt < maxAttempts)
            {
                try
                {
                    var (data, contentType) = await DownloadImageAsync(url);
                    return (data, contentType);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    attempt++;
                    await Task.Delay(200 * attempt);
                }
            }

            throw lastEx ?? new InvalidOperationException("Unknown download error");
        }

        private async Task<string?> ResolveTokenAsync()
        {
            try
            {
                if (_tokenProvider != null)
                    return await _tokenProvider();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] tokenProvider threw: {ex.Message}");
            }

            return _staticToken;
        }

        private async Task<(byte[]? data, string? contentType)> DownloadImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("url is null or empty", nameof(url));

            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) && uri.IsAbsoluteUri)
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    string? token = await ResolveTokenAsync();
                    Debug.WriteLine($"[ProfilePictureCache] Downloading absolute URL '{uri}' - token present: {(string.IsNullOrEmpty(token) ? "no" : "yes (masked)")}");
                    var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    if (!string.IsNullOrEmpty(token))
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    using var resp = await _httpClient.SendAsync(request);
                    if (resp.IsSuccessStatusCode)
                    {
                        var contentType = resp.Content.Headers.ContentType?.MediaType;
                        var data = await resp.Content.ReadAsByteArrayAsync();
                        return (data, contentType);
                    }

                    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenProvider != null)
                    {
                        var body = await SafeReadResponseBodyAsync(resp);
                        Debug.WriteLine($"[ProfilePictureCache] 401 received for '{uri}'. Response body: {body}");

                        var refreshedToken = await ResolveTokenAsync();
                        if (!string.IsNullOrEmpty(refreshedToken) && refreshedToken != token)
                        {
                            Debug.WriteLine("[ProfilePictureCache] Retrying download with refreshed token");
                            var retryReq = new HttpRequestMessage(HttpMethod.Get, uri);
                            retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
                            using var resp2 = await _httpClient.SendAsync(retryReq);
                            if (resp2.IsSuccessStatusCode)
                            {
                                var ct = resp2.Content.Headers.ContentType?.MediaType;
                                var d = await resp2.Content.ReadAsByteArrayAsync();
                                return (d, ct);
                            }
                            var body2 = await SafeReadResponseBodyAsync(resp2);
                            Debug.WriteLine($"[ProfilePictureCache] Retry failed: {resp2.StatusCode}. Body: {body2}");
                            resp2.EnsureSuccessStatusCode();
                        }

                        resp.EnsureSuccessStatusCode(); // will throw with 401
                    }

                    var respBody = await SafeReadResponseBodyAsync(resp);
                    Debug.WriteLine($"[ProfilePictureCache] Download failed {resp.StatusCode} for '{uri}'. Body: {respBody}");
                    resp.EnsureSuccessStatusCode(); // will throw
                }

                if (uri.Scheme == "pack")
                {
                    try
                    {
                        var info = System.Windows.Application.GetResourceStream(uri);
                        if (info?.Stream != null)
                        {
                            using var ms = new MemoryStream();
                            await info.Stream.CopyToAsync(ms);
                            var data = ms.ToArray();
                            return (data, null);
                        }
                        throw new FileNotFoundException("Pack resource not found", url);
                    }
                    catch (Exception ex)
                    {
                        throw new FileNotFoundException($"Failed to load pack resource: {url}", ex);
                    }
                }

                if (uri.Scheme == Uri.UriSchemeFile)
                {
                    var path = uri.LocalPath;
                    if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
                    var data = await File.ReadAllBytesAsync(path);
                    return (data, null);
                }

                throw new InvalidOperationException($"Unsupported URI scheme: {uri.Scheme}");
            }

            string requestUrl = url;
            if (requestUrl.StartsWith("/")) requestUrl = requestUrl.TrimStart('/');
            requestUrl = _serverBaseUrl + requestUrl;

            string? token2 = await ResolveTokenAsync();
            Debug.WriteLine($"[ProfilePictureCache] Downloading relative URL '{requestUrl}' - token present: {(string.IsNullOrEmpty(token2) ? "no" : "yes (masked)")}");

            var request2 = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (!string.IsNullOrEmpty(token2))
                request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);

            using var respFinal = await _httpClient.SendAsync(request2);
            if (respFinal.IsSuccessStatusCode)
            {
                var contentType2 = respFinal.Content.Headers.ContentType?.MediaType;
                var data2 = await respFinal.Content.ReadAsByteArrayAsync();
                return (data2, contentType2);
            }

            if (respFinal.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenProvider != null)
            {
                var b = await SafeReadResponseBodyAsync(respFinal);
                Debug.WriteLine($"[ProfilePictureCache] 401 received for '{requestUrl}'. Body: {b}");

                var refreshed = await ResolveTokenAsync();
                if (!string.IsNullOrEmpty(refreshed) && refreshed != token2)
                {
                    Debug.WriteLine("[ProfilePictureCache] Retrying relative request with refreshed token");
                    var retryReq2 = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    retryReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
                    using var respRetry = await _httpClient.SendAsync(retryReq2);
                    if (respRetry.IsSuccessStatusCode)
                    {
                        var ct = respRetry.Content.Headers.ContentType?.MediaType;
                        var d = await respRetry.Content.ReadAsByteArrayAsync();
                        return (d, ct);
                    }
                    var rb = await SafeReadResponseBodyAsync(respRetry);
                    Debug.WriteLine($"[ProfilePictureCache] Retry failed: {respRetry.StatusCode}. Body: {rb}");
                    respRetry.EnsureSuccessStatusCode();
                }

                respFinal.EnsureSuccessStatusCode(); // will throw with 401
            }

            var bodyFinal = await SafeReadResponseBodyAsync(respFinal);
            Debug.WriteLine($"[ProfilePictureCache] Download failed {respFinal.StatusCode} for '{requestUrl}'. Body: {bodyFinal}");
            respFinal.EnsureSuccessStatusCode();

            return (null, null);
        }

        private static async Task<string> SafeReadResponseBodyAsync(HttpResponseMessage resp)
        {
            try
            {
                if (resp.Content == null) return string.Empty;
                var s = await resp.Content.ReadAsStringAsync();
                return s ?? string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] SafeReadResponseBodyAsync failed: {ex.Message}");
                return string.Empty;
            }
        }

        private string? ExtensionFromContentType(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return null;
            return contentType.ToLower() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                _ => null
            };
        }

        private string? GetExtensionFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.RelativeOrAbsolute);
                var seg = uri.Segments.Length > 0 ? uri.Segments[^1] : null;
                if (seg != null && seg.Contains('.'))
                {
                    return Path.GetExtension(seg);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] GetExtensionFromUrl failed for '{url}': {ex.Message}");
            }
            return null;
        }

        private async Task SaveToDiskAsync(string path, byte[] data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);

                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var tmpPath = path + ".tmp";
                await File.WriteAllBytesAsync(tmpPath, data);

                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProfilePictureCache] SaveToDiskAsync delete existing file failed: {ex.Message}");
                }

                File.Move(tmpPath, path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] SaveToDiskAsync failed for {path}: {ex.Message}");
            }
        }

        private BitmapImage? LoadImageFromDisk(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.PreservePixelFormat;
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] LoadImageFromDisk failed for {path}: {ex.Message}");
                return null;
            }
        }

        private BitmapImage? LoadImageFromBytes(byte[] imageData)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(imageData))
                {
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.PreservePixelFormat;
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfilePictureCache] LoadImageFromBytes failed: {ex.Message}");
                return null;
            }
        }

        private void AddToMemoryCache(string cacheKey, BitmapImage image)
        {
            if (_memoryCache.Count >= MAX_MEMORY_CACHE_SIZE)
            {
                foreach (var key in _memoryCache.Keys)
                {
                    _memoryCache.TryRemove(key, out _);
                    break;
                }
            }

            _memoryCache.TryAdd(cacheKey, image);
        }

        public bool TryGetFromMemory(string pfpUrl, out BitmapImage? image)
        {
            image = null;
            if (string.IsNullOrEmpty(pfpUrl)) return false;
            try
            {
                var cacheKey = GetCacheKey(pfpUrl);
                return _memoryCache.TryGetValue(cacheKey, out image!);
            }
            catch (Exception ex) { Debug.WriteLine($"[ProfilePictureCache] TryGetFromMemory failed: {ex.Message}"); return false; }
        }

        #endregion Private Methods

        public void Dispose()
        {
            _httpClient?.Dispose();
            _memoryCache?.Clear();
        }
    }
}