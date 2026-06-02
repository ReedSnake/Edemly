using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Edemly.Client.DTOs;

namespace Edemly.Client.Services.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        public async Task<(bool Success, string? Url, string? Error)> UploadProfilePictureAsync(string filePath)
        {
            try
            {
                using var form = new MultipartFormDataContent();

                var fileStream = File.OpenRead(filePath);
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                form.Add(streamContent, "file", Path.GetFileName(filePath));

                var rel = "api/file/upload-profile-picture";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] POST {_httpClient.BaseAddress}{url} (multipart)");
                var response = await _httpClient.PostAsync(url, form);

                if (!response.IsSuccessStatusCode)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    return (false, null, "Failed to upload profile picture");
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = TryDeserialize<UploadResponseDto>(json);

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
                if (string.IsNullOrEmpty(pfpUrl))
                {
                    return (false, null, "pfpUrl is empty");
                }

                string requestUrl;

                // If absolute HTTP(S) or pack URI, use as-is. Otherwise use BuildUrl to combine with BaseAddress.
                if (pfpUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) || pfpUrl.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    requestUrl = pfpUrl;
                }
                else
                {
                    requestUrl = BuildUrl(pfpUrl);
                }

                var response = await _httpClient.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, "Failed to download profile picture");
                }

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
                var content = new MultipartFormDataContent();
                var fileStream = File.OpenRead(filePath);
                var streamContent = new StreamContent(fileStream);

                // Определяем content type по расширению
                var extension = Path.GetExtension(filePath).ToLower();
                var contentType = extension switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "file", Path.GetFileName(filePath));
                content.Add(new StringContent(chatId.ToString()), "chatId");

                var url = BuildUrl("api/Chat/upload-icon");
                System.Diagnostics.Debug.WriteLine($"[API] POST {url} (multipart) for chat {chatId}");
                
                var response = await _httpClient.PostAsync(url, content);

                // Освобождаем ресурсы после отправки
                streamContent.Dispose();
                fileStream.Dispose();
                content.Dispose();

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] UploadGroupIconAsync response: {json}");
                    
                    var result = TryDeserialize<UploadResponseDto>(json);

                    if (result != null && !string.IsNullOrEmpty(result.Url))
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Group icon uploaded successfully: {result.Url}");
                        return (true, result.Url, null);
                    }

                    return (false, null, "Failed to parse response or empty URL");
                }

                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[API] UploadGroupIconAsync failed: {error}");
                return (false, null, error);
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
                using var fileStream = File.OpenRead(filePath);
                using var streamContent = new StreamContent(fileStream);

                var fileName = Path.GetFileName(filePath);
                var extension = Path.GetExtension(filePath).ToLower();

                // determine content type
                string contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".pdf" => "application/pdf",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => "application/octet-stream"
                };

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(streamContent, "file", fileName);

                var url = BuildUrl("api/file/upload");
                System.Diagnostics.Debug.WriteLine($"[API] POST {url} (multipart)");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = TryDeserialize<UploadFileResponseDto>(json);

                    if (result != null)
                    {
                        return (true, result.Url, fileName, null);
                    }

                    return (false, null, null, "Failed to parse response");
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, null, null, error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] UploadFileAsync failed: {ex.Message}");
                return (false, null, null, ex.Message);
            }
        }
    }
}
