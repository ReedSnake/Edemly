namespace Edemly.Client.Api.Files;

public interface IFileApiClient
{
    public Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(string filePath);

    public Task<(bool Success, byte[]? ImageData, string? Error)> DownloadProfilePictureAsync(string pfpUrl);
    public Task<(bool Success, string? Url, string? Error)> UploadGroupIconAsync(int chatId, string filePath);
    public Task<(bool Success, string? Url, string? FileName, string? Error)> UploadFileAsync(string filePath);
}

