using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly Configuration.FileStorageSettings _settings;
        private readonly ILogger<FileStorageService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly long _maxFileSize;
        private readonly string? _publicBaseUrl;
        private readonly ITenantProvider _tenantProvider;
        private readonly ServerDbContext _serverDb;

        public FileStorageService(
            Configuration.FileStorageSettings settings,
            IWebHostEnvironment environment,
            ILogger<FileStorageService> logger,
            IPublicUrlProvider publicUrlProvider,
            ITenantProvider tenantProvider,
            ServerDbContext serverDb)
        {
            _settings = settings;
            _environment = environment;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _serverDb = serverDb;
            _maxFileSize = settings.MaxFileSizeMB * 1024 * 1024;

            string baseUrl = settings.BaseUrl ?? string.Empty;
            if (baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _publicBaseUrl = baseUrl.TrimEnd('/');
            }
            else
            {
                var publicUrl = publicUrlProvider?.GetPublicBaseUrl();
                if (!string.IsNullOrWhiteSpace(publicUrl))
                {
                    var trimmed = baseUrl.Trim('/');
                    _publicBaseUrl = string.IsNullOrEmpty(trimmed)
                        ? publicUrl.TrimEnd('/')
                        : publicUrl.TrimEnd('/') + "/" + trimmed;
                }
                else
                {
                    _publicBaseUrl = string.IsNullOrEmpty(baseUrl) ? null : baseUrl.TrimEnd('/');
                }
            }

            InitializeDirectories();
        }

        private void InitializeDirectories()
        {
            try
            {
                var webRoot = _environment.WebRootPath ?? Directory.GetCurrentDirectory();
                var baseUploads = Path.Combine(webRoot, _settings.StoragePath);

                if (!Directory.Exists(baseUploads)) Directory.CreateDirectory(baseUploads);
                Directory.CreateDirectory(Path.Combine(baseUploads, _settings.ProfilePicturesFolder));
                Directory.CreateDirectory(Path.Combine(baseUploads, _settings.FilesFolder));

                _logger.LogInformation("File storage directories initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize file storage directories");
            }
        }

        private string GetTenantFolder()
        {
            return _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null
                ? _tenantProvider.CurrentCompany.Name
                : string.Empty;
        }

        private string GetFullPath(params string[] parts)
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var allParts = new[] { webRoot }.Concat(parts).ToArray();
            return Path.Combine(allParts);
        }

        public async Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(
            int userId, Stream fileStream, string fileName)
        {
            try
            {
                if (fileStream.Length > _maxFileSize)
                    return (false, null, $"File size exceeds {_maxFileSize / 1024 / 1024} MB");

                var extension = Path.GetExtension(fileName);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var safeFileName = $"user_{userId}_{timestamp}_{uniqueId}{extension}";

                var tenantFolder = GetTenantFolder();
                var relativePath = Path.Combine(tenantFolder, _settings.ProfilePicturesFolder, safeFileName).Replace('\\', '/');
                var fullPath = GetFullPath(_settings.StoragePath, tenantFolder, _settings.ProfilePicturesFolder, safeFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                using (var outStream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(outStream);
                }

                string url;
                if (!string.IsNullOrEmpty(_publicBaseUrl))
                {
                    var publicPath = string.IsNullOrEmpty(tenantFolder)
                        ? $"{_settings.ProfilePicturesFolder.Trim('/')}/{safeFileName}"
                        : $"{tenantFolder}/{_settings.ProfilePicturesFolder.Trim('/')}/{safeFileName}";

                    url = $"{_publicBaseUrl}/{publicPath}";
                }
                else
                {
                    url = $"/{relativePath.TrimStart('/')}";
                }

                _logger.LogInformation("Uploaded profile picture for user {UserId}: {Url}", userId, url);
                return (true, url, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture for user {UserId}", userId);
                return (false, null, "Upload failed");
            }
        }

        public async Task<(bool Success, string? Url, string? Error)> UploadFileAsync(
            int userId, Stream fileStream, string fileName, string contentType)
        {
            try
            {
                if (fileStream.Length > _maxFileSize)
                    return (false, null, $"File size exceeds {_maxFileSize / 1024 / 1024} MB");

                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var safeName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "_").Replace("..", "");
                var extension = Path.GetExtension(fileName);
                var safeFileName = $"user_{userId}_{timestamp}_{safeName}{extension}";

                var tenantFolder = GetTenantFolder();
                var relativePath = Path.Combine(tenantFolder, _settings.FilesFolder, safeFileName).Replace('\\', '/');
                var fullPath = GetFullPath(_settings.StoragePath, tenantFolder, _settings.FilesFolder, safeFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                using (var outStream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(outStream);
                }

                string url;
                if (!string.IsNullOrEmpty(_publicBaseUrl))
                {
                    var publicPath = string.IsNullOrEmpty(tenantFolder)
                        ? $"{_settings.FilesFolder.Trim('/')}/{safeFileName}"
                        : $"{tenantFolder}/{_settings.FilesFolder.Trim('/')}/{safeFileName}";

                    url = $"{_publicBaseUrl}/{publicPath}";
                }
                else
                {
                    url = $"/{relativePath.TrimStart('/')}";
                }

                _logger.LogInformation("Uploaded file for user {UserId}: {Url}", userId, url);
                return (true, url, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for user {UserId}", userId);
                return (false, null, "Upload failed");
            }
        }

        public async Task<(bool Success, string? Error)> DeleteFileAsync(string fileUrl)
        {
            try
            {
                var relativePath = ParseRelativePath(fileUrl);

                if (!_tenantProvider.IsTenant)
                {
                    var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0 && await _serverDb.Companies.AnyAsync(c => c.Name == segments[0]))
                        return (false, "Access denied: master cannot delete tenant files");
                }

                var fullPath = Path.Combine(_environment.WebRootPath ?? Directory.GetCurrentDirectory(), _settings.StoragePath, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted file: {Url}", fileUrl);
                    return (true, null);
                }

                return (false, "File not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {Url}", fileUrl);
                return (false, "Delete failed");
            }
        }

        public async Task<Stream?> GetFileAsync(string fileUrl)
        {
            try
            {
                var relativePath = ParseRelativePath(fileUrl);

                if (!_tenantProvider.IsTenant)
                {
                    var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0 && await _serverDb.Companies.AnyAsync(c => c.Name == segments[0]))
                    {
                        _logger.LogWarning("Access denied: master attempting to access tenant file {Path}", relativePath);
                        return null;
                    }
                }

                var fullPath = Path.Combine(_environment.WebRootPath ?? Directory.GetCurrentDirectory(), _settings.StoragePath, relativePath);

                if (File.Exists(fullPath))
                    return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file: {Url}", fileUrl);
                return null;
            }
        }

        private string ParseRelativePath(string fileUrl)
        {
            string path = fileUrl;
            if (!string.IsNullOrEmpty(_publicBaseUrl) && fileUrl.StartsWith(_publicBaseUrl, StringComparison.OrdinalIgnoreCase))
                path = fileUrl.Substring(_publicBaseUrl.Length);
            else if (!string.IsNullOrEmpty(_settings.BaseUrl) && fileUrl.StartsWith(_settings.BaseUrl, StringComparison.OrdinalIgnoreCase))
                path = fileUrl.Substring(_settings.BaseUrl.Length);

            return path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        }
    }
}