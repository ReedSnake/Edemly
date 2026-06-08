using Edemly.Client.Api.Core;
using Edemly.Contracts.Files;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Edemly.Client.Api.Files;

public sealed class FileApiClient : ApiClientBase, IFileApiClient
{
    public FileApiClient(ApiClientContext context) : base(context)
    {
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(string filePath)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(FileContentTypeResolver.GetImageContentType(filePath));

            form.Add(streamContent, "file", Path.GetFileName(filePath));

            var url = UrlHelper.BuildRelativeUrl("api/users/me/profile-picture");
            var response = await HttpClient.PostAsync(url, form);

            if (!response.IsSuccessStatusCode)
                return (false, null, "Failed to upload profile picture");

            var result = await ReadJsonAsync<UploadProfilePictureResponseDto>(response);

            return (true, result?.Url, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] UploadProfilePictureAsync failed: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, byte[]? ImageData, string? Error)> DownloadProfilePictureAsync(string pfpUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pfpUrl))
                return (false, null, "pfpUrl is empty");

            var requestUrl =
                pfpUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                pfpUrl.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
                    ? pfpUrl
                    : UrlHelper.BuildRelativeUrl(pfpUrl);

            var response = await HttpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
                return (false, null, "Failed to download profile picture");

            var imageData = await response.Content.ReadAsByteArrayAsync();

            return (true, imageData, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Url, string? Error)> UploadGroupIconAsync(int chatId, string filePath)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(FileContentTypeResolver.GetImageContentType(filePath));

            content.Add(streamContent, "file", Path.GetFileName(filePath));

            var url = UrlHelper.BuildRelativeUrl($"api/chats/{chatId}/icon");
            var response = await HttpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, error);
            }

            var result = await ReadJsonAsync<UploadProfilePictureResponseDto>(response);

            if (result == null || string.IsNullOrWhiteSpace(result.Url))
                return (false, null, "Failed to parse response or empty URL");

            return (true, result.Url, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] UploadGroupIconAsync exception: {ex.Message}");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Url, string? FileName, string? Error)> UploadFileAsync(string filePath)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(filePath);
            using var streamContent = new StreamContent(fileStream);

            var fileName = Path.GetFileName(filePath);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue(FileContentTypeResolver.GetFileContentType(filePath));
            content.Add(streamContent, "file", fileName);

            var url = UrlHelper.BuildRelativeUrl("api/files");
            var response = await HttpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, null, null, error);
            }

            var result = await ReadJsonAsync<UploadFileResponseDto>(response);

            if (result == null)
                return (false, null, null, "Failed to parse response");

            return (true, result.Url, fileName, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] UploadFileAsync failed: {ex.Message}");
            return (false, null, null, ex.Message);
        }
    }
}