#nullable disable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Edemly.Client.Caching
{
    public class FileCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, string> _filePathCache;
        private readonly string _diskCachePath;
        private readonly HttpClient _httpClient;
        private readonly string _serverBaseUrl; // normalized base URL with trailing slash

        private readonly Func<Task<string?>>? _tokenProvider;
        private string? _staticToken;

        private readonly ConcurrentDictionary<string, Task<string>> _downloadTasks = new();

        public event Action<string>? DownloadStarted;

        public event Action<string, string>? DownloadCompleted; // (fileUrl, localPath)

        public event Action<string, Exception>? DownloadFailed;

        public FileCache(string serverBaseUrl, string cacheScope = "personal") : this(serverBaseUrl, null, cacheScope)
        {
        }

        public FileCache(string serverBaseUrl, Func<Task<string?>>? tokenProvider, string cacheScope = "personal")
        {
            _filePathCache = new ConcurrentDictionary<string, string>();

            var safeScope = MakeSafeFolderName(cacheScope);
            _diskCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Edemly",
                "cache",
                "files",
                safeScope);

            try { Directory.CreateDirectory(_diskCachePath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to create cache dir: {ex.Message}"); }

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
            if (!url.Contains("://")) url = "https://" + url;
            if (!url.EndsWith('/')) url += '/';
            return url;
        }

        private async Task<string?> ResolveTokenAsync()
        {
            try
            {
                if (_tokenProvider != null)
                    return await _token_provider_wrapperAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileCache] tokenProvider threw: {ex.Message}");
            }

            return _staticToken;
        }

        private async Task<string?> _token_provider_wrapperAsync()
        {
            return await _tokenProvider!();
        }

        public async Task<string> GetOrDownloadAsync(string fileUrl, string originalFileName)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return null;

            var cacheKey = GetCacheKey(fileUrl);

            try
            {
                if (_filePathCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
                {
                    return cachedPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileCache] Memory cache check failed: {ex.Message}");
            }

            var diskPath = FindDiskCachePath(cacheKey);
            if (diskPath != null && File.Exists(diskPath))
            {
                try { _filePathCache.TryAdd(cacheKey, diskPath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to add disk cache path: {ex.Message}"); }
                return diskPath;
            }

            var task = _download_tasks_get(cacheKey, fileUrl, originalFileName);
            try
            {
                var local = await task;
                return local;
            }
            finally
            {
                _downloadTasks.TryRemove(cacheKey, out _);
            }
        }

        private Task<string> _download_tasks_get(string cacheKey, string fileUrl, string originalFileName)
        {
            return _downloadTasks.GetOrAdd(cacheKey, _ => DownloadAndSaveAsync(fileUrl, originalFileName, cacheKey));
        }

        private async Task<string> DownloadAndSaveAsync(string fileUrl, string originalFileName, string cacheKey)
        {
            DownloadStarted?.Invoke(fileUrl);

            try
            {
                var (data, contentType) = await DownloadFileWithRetriesAsync(fileUrl, maxAttempts: 3);
                if (data == null || data.Length == 0)
                    throw new InvalidOperationException("Downloaded data is empty");

                var ext = Path.GetExtension(originalFileName);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = ExtensionFromContentType(contentType) ?? GetExtensionFromUrl(fileUrl) ?? ".bin";
                }

                var diskPath = GetDiskCachePath(cacheKey, ext);
                await SaveToDiskAsync(diskPath, data);

                try { _filePathCache.TryAdd(cacheKey, diskPath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to add download cache path: {ex.Message}"); }

                DownloadCompleted?.Invoke(fileUrl, diskPath);
                return diskPath;
            }
            catch (Exception ex)
            {
                DownloadFailed?.Invoke(fileUrl, ex);
                Debug.WriteLine($"[FileCache] DownloadAndSaveAsync failed for {fileUrl}: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CacheLocalFileAsync(string filePath)
        {
            try
            {
                var fileData = await File.ReadAllBytesAsync(filePath);
                var cacheKey = GetCacheKey(filePath);
                var fileName = Path.GetFileName(filePath);
                var diskPath = GetDiskCachePath(cacheKey, Path.GetExtension(fileName));

                await SaveToDiskAsync(diskPath, fileData);
                try { _filePathCache.TryAdd(cacheKey, diskPath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to add local cache path: {ex.Message}"); }

                return diskPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileCache] CacheLocalFileAsync failed: {ex.Message}");
                return null;
            }
        }

        public void InvalidateCache(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return;

            var cacheKey = GetCacheKey(fileUrl);

            if (_file_path_cache_try_remove(cacheKey, out var filePath))
            {
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to delete cached file {filePath}: {ex.Message}"); }
                }
            }

            var disk = FindDiskCachePath(cacheKey);
            if (disk != null && File.Exists(disk))
            {
                try { File.Delete(disk); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to delete disk cache {disk}: {ex.Message}"); }
            }
        }

        private bool _file_path_cache_try_remove(string cacheKey, out string filePath)
        {
            try { return _filePathCache.TryRemove(cacheKey, out filePath); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to remove cache key: {ex.Message}"); filePath = null; return false; }
        }

        public void ClearAll()
        {
            try { _filePathCache.Clear(); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to clear memory cache: {ex.Message}"); }
            try
            {
                if (Directory.Exists(_diskCache_path()))
                {
                    Directory.Delete(_diskCache_path(), true);
                    Directory.CreateDirectory(_diskCache_path());
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[FileCache] ClearAll failed: {ex.Message}"); }
        }

        private string _diskCache_path() => _diskCachePath;

        #region Private helpers

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
            if (string.IsNullOrEmpty(extension)) extension = ".bin";
            if (!extension.StartsWith('.')) extension = "." + extension;
            return Path.Combine(_diskCachePath, $"{cacheKey}{extension}");
        }

        private string? FindDiskCachePath(string cacheKey)
        {
            try
            {
                var dir = new DirectoryInfo(_diskCachePath);
                var files = dir.GetFiles(cacheKey + ".*");
                if (files.Length > 0)
                {
                    var newest = files.OrderByDescending(f => f.LastWriteTimeUtc).First();
                    return newest.FullName;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[FileCache] FindDiskCachePath failed: {ex.Message}"); }
            return null;
        }

        private async Task<(byte[] data, string? contentType)> DownloadFileWithRetriesAsync(string url, int maxAttempts = 3)
        {
            int attempt = 0;
            Exception last = null;
            while (attempt < maxAttempts)
            {
                try
                {
                    var res = await DownloadFileAsync(url);
                    return res;
                }
                catch (Exception ex)
                {
                    last = ex;
                    attempt++;
                    await Task.Delay(200 * attempt);
                }
            }

            throw last ?? new InvalidOperationException("Download failed");
        }

        private async Task<(byte[] data, string? contentType)> DownloadFileAsync(string url)
        {
            string requestUrl = url;
            if (!requestUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (requestUrl.StartsWith('/')) requestUrl = requestUrl.TrimStart('/');
                requestUrl = _serverBaseUrl + requestUrl;
            }

            string? token = await ResolveTokenAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _httpClient.SendAsync(request);
            if (resp.IsSuccessStatusCode)
            {
                var contentType = resp.Content.Headers.ContentType?.MediaType;
                var data = await resp.Content.ReadAsByteArrayAsync();
                return (data, contentType);
            }

            var body = string.Empty;
            try { body = await resp.Content.ReadAsStringAsync(); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to read error response body: {ex.Message}"); }
            Debug.WriteLine($"[FileCache] Download failed {resp.StatusCode} for '{requestUrl}'. Body: {body}");

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenProvider != null)
            {
                try
                {
                    var refreshed = await ResolveTokenAsync();
                    if (!string.IsNullOrEmpty(refreshed) && refreshed != token)
                    {
                        Debug.WriteLine("[FileCache] Retrying download with refreshed token");
                        var retryReq = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
                        using var resp2 = await _httpClient.SendAsync(retryReq);
                        if (resp2.IsSuccessStatusCode)
                        {
                            var ct = resp2.Content.Headers.ContentType?.MediaType;
                            var d = await resp2.Content.ReadAsByteArrayAsync();
                            return (d, ct);
                        }
                        var body2 = string.Empty;
                        try { body2 = await resp2.Content.ReadAsStringAsync(); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Failed to read retry response body: {ex.Message}"); }
                        Debug.WriteLine($"[FileCache] Retry failed: {resp2.StatusCode}. Body: {body2}");
                        resp2.EnsureSuccessStatusCode();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileCache] Retry after 401 failed: {ex.Message}");
                }

                resp.EnsureSuccessStatusCode(); // will throw 401
            }

            resp.EnsureSuccessStatusCode();
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
                Debug.WriteLine($"[FileCache] SafeReadResponseBodyAsync failed: {ex.Message}");
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
                "application/pdf" => ".pdf",
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
                    return Path.GetExtension(seg);
            }
            catch (Exception ex) { Debug.WriteLine($"[FileCache] GetExtensionFromUrl failed for '{url}': {ex.Message}"); }
            return null;
        }

        private async Task SaveToDiskAsync(string path, byte[] data)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var tmp = path + ".tmp";
                await File.WriteAllBytesAsync(tmp, data);
                try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { Debug.WriteLine($"[FileCache] SaveToDiskAsync delete failed for {path}: {ex.Message}"); }
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileCache] SaveToDiskAsync failed for {path}: {ex.Message}");
            }
        }

        #endregion Private helpers

        public void Dispose()
        {
            _httpClient?.Dispose();
            try { _filePathCache?.Clear(); } catch (Exception ex) { Debug.WriteLine($"[FileCache] Dispose cache clear failed: {ex.Message}"); }
        }
    }
}