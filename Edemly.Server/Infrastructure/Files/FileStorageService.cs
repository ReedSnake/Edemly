using Edemly.Server.Api.Middleware;
using Edemly.Server.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Infrastructure.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Edemly.Server.Infrastructure.Files
{
    public class FileStorageService : IFileStorageService
    {
        private static readonly SemaphoreSlim MinioBucketLock = new(1, 1);
        private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

        private readonly FileStorageSettings _settings;
        private readonly ILogger<FileStorageService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly long _maxFileSize;
        private readonly string? _publicBaseUrl;
        private readonly ITenantProvider _tenantProvider;
        private readonly ServerDbContext _serverDb;
        private readonly IMinioClient? _minioClient;
        private readonly bool _useMinio;
        private readonly string _minioBucketName;
        private readonly string _minioObjectPrefix;

        public FileStorageService(
            FileStorageSettings settings,
            IWebHostEnvironment environment,
            ILogger<FileStorageService> logger,
            IPublicUrlProvider publicUrlProvider,
            ITenantProvider tenantProvider,
            ServerDbContext serverDb,
            IEnumerable<IMinioClient> minioClients)
        {
            _settings = settings;
            _environment = environment;
            _logger = logger;
            _tenantProvider = tenantProvider;
            _serverDb = serverDb;
            _maxFileSize = settings.MaxFileSizeMB * 1024 * 1024;
            _useMinio = settings.UseMinio;
            _minioClient = minioClients.FirstOrDefault();
            _minioBucketName = settings.Minio.BucketName;
            _minioObjectPrefix = NormalizeObjectPrefix(settings.Minio.ObjectPrefix);

            if (_useMinio && _minioClient == null)
            {
                throw new InvalidOperationException("MinIO file storage is enabled, but IMinioClient is not registered.");
            }

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

            if (!_useMinio)
            {
                InitializeDirectories();
            }
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
                var relativePath = CombineUrlSegments(tenantFolder, _settings.ProfilePicturesFolder, safeFileName);

                if (_useMinio)
                {
                    await UploadMinioObjectAsync(
                        relativePath,
                        fileStream,
                        ResolveContentType(fileName, "application/octet-stream"));
                }
                else
                {
                    var fullPath = GetFullPath(_settings.StoragePath, tenantFolder, _settings.ProfilePicturesFolder, safeFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                    using var outStream = new FileStream(fullPath, FileMode.Create);
                    await fileStream.CopyToAsync(outStream);
                }

                var url = BuildPublicUrl(relativePath);
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
                var relativePath = CombineUrlSegments(tenantFolder, _settings.FilesFolder, safeFileName);

                if (_useMinio)
                {
                    await UploadMinioObjectAsync(relativePath, fileStream, contentType);
                }
                else
                {
                    var fullPath = GetFullPath(_settings.StoragePath, tenantFolder, _settings.FilesFolder, safeFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                    using var outStream = new FileStream(fullPath, FileMode.Create);
                    await fileStream.CopyToAsync(outStream);
                }

                var url = BuildPublicUrl(relativePath);
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

                if (!await CanAccessRelativePathAsync(relativePath))
                {
                    return (false, "Access denied");
                }

                if (_useMinio)
                {
                    return await DeleteMinioObjectAsync(relativePath);
                }

                var fullPath = Path.Combine(
                    _environment.WebRootPath ?? Directory.GetCurrentDirectory(),
                    _settings.StoragePath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

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

                if (!await CanAccessRelativePathAsync(relativePath))
                {
                    _logger.LogWarning("Access denied while reading file {Path}", relativePath);
                    return null;
                }

                if (_useMinio)
                {
                    return await GetMinioObjectAsync(relativePath);
                }

                var fullPath = Path.Combine(
                    _environment.WebRootPath ?? Directory.GetCurrentDirectory(),
                    _settings.StoragePath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));

                return File.Exists(fullPath)
                    ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file: {Url}", fileUrl);
                return null;
            }
        }

        private async Task UploadMinioObjectAsync(string relativePath, Stream fileStream, string? contentType)
        {
            await EnsureMinioBucketAsync();

            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_minioBucketName)
                .WithObject(BuildMinioObjectName(relativePath))
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            await _minioClient!.PutObjectAsync(putObjectArgs);
        }

        private async Task<Stream?> GetMinioObjectAsync(string relativePath)
        {
            try
            {
                var memoryStream = new MemoryStream();
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_minioBucketName)
                    .WithObject(BuildMinioObjectName(relativePath))
                    .WithCallbackStream(stream => stream.CopyTo(memoryStream));

                await _minioClient!.GetObjectAsync(getObjectArgs);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (ObjectNotFoundException)
            {
                return null;
            }
            catch (BucketNotFoundException)
            {
                return null;
            }
        }

        private async Task<(bool Success, string? Error)> DeleteMinioObjectAsync(string relativePath)
        {
            var objectName = BuildMinioObjectName(relativePath);
            if (!await MinioObjectExistsAsync(objectName))
            {
                return (false, "File not found");
            }

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_minioBucketName)
                .WithObject(objectName);

            await _minioClient!.RemoveObjectAsync(removeObjectArgs);
            _logger.LogInformation("Deleted MinIO object: {ObjectName}", objectName);
            return (true, null);
        }

        private async Task<bool> MinioObjectExistsAsync(string objectName)
        {
            try
            {
                var statObjectArgs = new StatObjectArgs()
                    .WithBucket(_minioBucketName)
                    .WithObject(objectName);

                await _minioClient!.StatObjectAsync(statObjectArgs);
                return true;
            }
            catch (ObjectNotFoundException)
            {
                return false;
            }
            catch (BucketNotFoundException)
            {
                return false;
            }
        }

        private async Task EnsureMinioBucketAsync()
        {
            if (!_settings.Minio.AutoCreateBucket)
            {
                return;
            }

            await MinioBucketLock.WaitAsync();
            try
            {
                var bucketExistsArgs = new BucketExistsArgs()
                    .WithBucket(_minioBucketName);

                var exists = await _minioClient!.BucketExistsAsync(bucketExistsArgs);
                if (exists)
                {
                    return;
                }

                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_minioBucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs);
            }
            finally
            {
                MinioBucketLock.Release();
            }
        }

        private async Task<bool> CanAccessRelativePathAsync(string relativePath)
        {
            if (_tenantProvider.IsTenant)
            {
                return true;
            }

            var firstSegment = relativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(firstSegment) ||
                !await _serverDb.Companies.AnyAsync(c => c.Name == firstSegment);
        }

        private string BuildPublicUrl(string relativePath)
        {
            if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            {
                return $"{_publicBaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
            }

            return "/" + relativePath.TrimStart('/');
        }

        private string BuildMinioObjectName(string relativePath)
        {
            return CombineUrlSegments(_minioObjectPrefix, relativePath);
        }

        private string ParseRelativePath(string fileUrl)
        {
            var path = fileUrl.Split('?', 2)[0];

            if (Uri.TryCreate(fileUrl, UriKind.Absolute, out var absoluteUri))
            {
                path = absoluteUri.AbsolutePath;
            }
            else if (!string.IsNullOrEmpty(_publicBaseUrl) &&
                path.StartsWith(_publicBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(_publicBaseUrl.Length);
            }

            var baseUrl = _settings.BaseUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var basePath = Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                    ? baseUri.AbsolutePath
                    : baseUrl;

                if (path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(basePath.Length);
                }
            }

            path = path.TrimStart('/').Replace('\\', '/');
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && string.Equals(segments[1], "uploads", StringComparison.OrdinalIgnoreCase))
            {
                return CombineUrlSegments(segments[0], string.Join("/", segments.Skip(2)));
            }

            return path;
        }

        private static string CombineUrlSegments(params string?[] parts)
        {
            return string.Join(
                "/",
                parts
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Select(part => part!.Trim().Trim('/').Replace('\\', '/')));
        }

        private static string NormalizeObjectPrefix(string? prefix)
        {
            return string.IsNullOrWhiteSpace(prefix)
                ? string.Empty
                : prefix.Trim().Trim('/').Replace('\\', '/');
        }

        private static string ResolveContentType(string fileName, string fallback)
        {
            return ContentTypeProvider.TryGetContentType(fileName, out var contentType)
                ? contentType
                : fallback;
        }
    }
}
