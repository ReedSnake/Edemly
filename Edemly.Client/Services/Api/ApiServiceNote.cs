using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Edemly.Client.DTOs;

namespace Edemly.Client.Services.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        public async Task<string?> GetContactNoteAsync(int userId)
        {
            try
            {
                // First check local per-creator file
                var localPath = GetLocalNotesPath();

                if (File.Exists(localPath))
                {
                    var json = await File.ReadAllTextAsync(localPath);
                    var root = TryDeserialize<Dictionary<string, Dictionary<int, string>>>(json) ?? new Dictionary<string, Dictionary<int, string>>();

                    var creatorKey = GetCreatorKey();
                    if (!string.IsNullOrEmpty(creatorKey) && root.TryGetValue(creatorKey, out var notesDict))
                    {
                        if (notesDict.TryGetValue(userId, out var note))
                            return note;
                    }
                }

                var url = BuildUrl($"api/user/{userId}/note");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var serverJson = await response.Content.ReadAsStringAsync();
                var result = TryDeserialize<NoteResponseDto>(serverJson);

                return result?.Note;
            }
            catch (Exception)
            {
                try
                {
                    var localPath = GetLocalNotesPath();
                    if (File.Exists(localPath))
                    {
                        var json = await File.ReadAllTextAsync(localPath);
                        var root = TryDeserialize<Dictionary<string, Dictionary<int, string>>>(json) ?? new Dictionary<string, Dictionary<int, string>>();
                        var creatorKey = GetCreatorKey();
                        if (!string.IsNullOrEmpty(creatorKey) && root.TryGetValue(creatorKey, out var notesDict))
                        {
                            return notesDict.TryGetValue(userId, out var note) ? note : null;
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                return null;
            }
        }

        public async Task<bool> SaveContactNoteAsync(int userId, string noteText)
        {
            try
            {
                var localPath = GetLocalNotesPath();
                var root = new Dictionary<string, Dictionary<int, string>>();

                if (File.Exists(localPath))
                {
                    var json = await File.ReadAllTextAsync(localPath);
                    root = TryDeserialize<Dictionary<string, Dictionary<int, string>>>(json) ?? new Dictionary<string, Dictionary<int, string>>();
                }

                var creatorKey = GetCreatorKey();
                if (string.IsNullOrEmpty(creatorKey)) return false;

                if (!root.ContainsKey(creatorKey))
                    root[creatorKey] = new Dictionary<int, string>();

                var notesDict = root[creatorKey];

                if (!notesDict.ContainsKey(userId) && notesDict.Count >= 5)
                {
                    // check server-side count endpoint if available
                    try
                    {
                        var resp = await _httpClient.GetAsync(BuildUrl("api/note/count"));
                        if (resp.IsSuccessStatusCode)
                        {
                            var cntJson = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(cntJson);
                            if (doc.RootElement.TryGetProperty("count", out var cntEl))
                            {
                                var serverCount = cntEl.GetInt32();
                                if (serverCount >= 5)
                                    return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ApiServiceNote] Note count check failed: {ex}");
                    }
                }

                notesDict[userId] = noteText;

                var newJson = JsonSerializer.Serialize(root);
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                await File.WriteAllTextAsync(localPath, newJson);

                // push to server
                try
                {
                    var requestBody = new { UserId = userId, NoteText = noteText };
                    var serverJson = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(serverJson, Encoding.UTF8, "application/json");

                    await _httpClient.PostAsync(BuildUrl("api/note/create"), content);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiServiceNote] Push note to server failed: {ex}");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteContactNoteAsync(int userId)
        {
            try
            {
                var localPath = GetLocalNotesPath();

                if (File.Exists(localPath))
                {
                    var json = await File.ReadAllTextAsync(localPath);
                    var root = TryDeserialize<Dictionary<string, Dictionary<int, string>>>(json) ?? new Dictionary<string, Dictionary<int, string>>();
                    var creatorKey = GetCreatorKey();
                    if (!string.IsNullOrEmpty(creatorKey) && root.TryGetValue(creatorKey, out var notesDict))
                    {
                        if (notesDict.Remove(userId))
                        {
                            var newJson = JsonSerializer.Serialize(root);
                            await File.WriteAllTextAsync(localPath, newJson);
                        }
                    }
                }

                try
                {
                    await _httpClient.DeleteAsync(BuildUrl($"api/user/{userId}/note"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiServiceNote] Delete note request failed: {ex}");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Task<int> GetNotesCountAsync()
        {
            try
            {
                var localPath = GetLocalNotesPath();

                if (File.Exists(localPath))
                {
                    var json = File.ReadAllText(localPath);
                    var root = TryDeserialize<Dictionary<string, Dictionary<int, string>>>(json) ?? new Dictionary<string, Dictionary<int, string>>();
                    var creatorKey = GetCreatorKey();
                    if (!string.IsNullOrEmpty(creatorKey) && root.TryGetValue(creatorKey, out var notesDict))
                        return Task.FromResult(notesDict.Count);
                }

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiServiceNote] GetNotesCountAsync failed: {ex}");
                return Task.FromResult(0);
            }
        }

        private string GetLocalNotesPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "uchat", "notes", "contact_notes.json");
        }
    }
}
