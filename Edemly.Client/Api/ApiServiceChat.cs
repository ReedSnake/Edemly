using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Edemly.Contracts.Chats;
using Edemly.Contracts.ChatMembers;
using Edemly.Client.Api;

namespace Edemly.Client.Services.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        public async Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int page = 1, int pageSize = 50)
        {
            try
            {
                var rel = $"api/message/chat/{chatId}?page={page}&pageSize={pageSize}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<MessageDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var messages = TryDeserialize<List<MessageDto>>(json);

                return messages ?? new List<MessageDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetChatMessagesAsync failed: {ex.Message}");
                return new List<MessageDto>();
            }
        }

        public async Task<List<ChatDto>> GetMyChatsAsync()
        {
            try
            {
                var rel = "api/chat/my-chats";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<ChatDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var chats = TryDeserialize<List<ChatDto>>(json);

                return chats ?? new List<ChatDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetMyChatsAsync failed: {ex.Message}");
                return new List<ChatDto>();
            }
        }

        public async Task<ChatDto?> CreateOrGetPrivateChatAsync(int userId)
        {
            try
            {
                var requestBody = new CreatePrivateChatDto { UserId = userId };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var rel = "api/chat/create-private";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] POST {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = TryDeserialize<CreateChatResponseDto>(responseJson);

                return result?.Chat;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] CreateOrGetPrivateChatAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<ChatDto?> CreateGroupChatAsync(string groupName, List<int> participantIds)
        {
            try
            {
                var request = new CreateGroupChatDto
                {
                    GroupName = groupName,
                    ParticipantIds = participantIds
                };

                var json = JsonSerializer.Serialize(request);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var rel = "api/chat/create-group";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] POST {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.PostAsync(url, content);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = TryDeserialize<CreateGroupChatResponseDto>(responseContent);

                    return result?.Chat;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] CreateGroupChatAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<ChatDto?> GetChatByIdAsync(int chatId)
        {
            try
            {
                var rel = $"api/chat/{chatId}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var chatDto = TryDeserialize<ChatDto>(json);

                return chatDto;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetChatByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId)
        {
            try
            {
                var rel = $"api/chatmember/list/{chatId}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<ChatMemberDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var members = TryDeserialize<List<ChatMemberDto>>(json);

                return members ?? new List<ChatMemberDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetChatMembersAsync failed: {ex.Message}");
                return new List<ChatMemberDto>();
            }
        }

        private string GetCreatorKey()
        {
            return App.CurrentUserId.HasValue ? App.CurrentUserId.Value.ToString() : string.Empty;
        }

        public async Task<(bool Success, string? Error)> UpdateChatAsync(int chatId, string? name = null, string? description = null, string? iconUrl = null)
        {
            try
            {
                var updateDto = new UpdateChatDto
                {
                    Id = chatId,
                    Name = name,
                    Description = description,
                    IconUrl = iconUrl
                };

                var url = BuildUrl("api/Chat/update");
                var response = await _httpClient.PutAsJsonAsync(url, updateDto);

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
