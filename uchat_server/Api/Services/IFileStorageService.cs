namespace uchat_server.Api.Services
{
    public interface IFileStorageService
    {
        Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(int userId, Stream fileStream, string fileName);
        Task<(bool Success, string? Url, string? Error)> UploadFileAsync(int userId, Stream fileStream, string fileName, string contentType);
        Task<(bool Success, string? Error)> DeleteFileAsync(string fileUrl);
        Task<Stream?> GetFileAsync(string fileUrl);
    }
}