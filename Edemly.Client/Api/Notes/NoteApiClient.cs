using Edemly.Client.Api.Core;
using Edemly.Contracts.Notes;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Edemly.Client.Api.Notes;

public sealed class NoteApiClient : ApiClientBase, INoteApiClient
{
    private readonly Func<string> _creatorKeyProvider;

    public NoteApiClient(
    ApiClientContext context,
    Func<string> creatorKeyProvider)
    : base(context)
    {
        _creatorKeyProvider = creatorKeyProvider;
    }

    public async Task<string?> GetContactNoteAsync(int userId)
    {
        try
        {
            var localPath = GetLocalNotesPath();

            if (File.Exists(localPath))
            {
                var json = await File.ReadAllTextAsync(localPath);
                var root = DeserializeLocalNotes(json);

                var creatorKey = GetCreatorKey();

                if (!string.IsNullOrEmpty(creatorKey) &&
                    root.TryGetValue(creatorKey, out var notesDict) &&
                    notesDict.TryGetValue(userId, out var note))
                {
                    return note;
                }
            }

            var url = UrlHelper.BuildRelativeUrl($"api/user/{userId}/note");
            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await ReadJsonAsync<NoteResponseDto>(response);

            return result?.Note;
        }
        catch
        {
            try
            {
                var localPath = GetLocalNotesPath();

                if (!File.Exists(localPath))
                    return null;

                var json = await File.ReadAllTextAsync(localPath);
                var root = DeserializeLocalNotes(json);

                var creatorKey = GetCreatorKey();

                if (!string.IsNullOrEmpty(creatorKey) &&
                    root.TryGetValue(creatorKey, out var notesDict))
                {
                    return notesDict.TryGetValue(userId, out var note) ? note : null;
                }
            }
            catch
            {
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
                root = DeserializeLocalNotes(json);
            }

            var creatorKey = GetCreatorKey();

            if (string.IsNullOrEmpty(creatorKey))
                return false;

            if (!root.ContainsKey(creatorKey))
                root[creatorKey] = new Dictionary<int, string>();

            var notesDict = root[creatorKey];

            if (!notesDict.ContainsKey(userId) && notesDict.Count >= 5)
            {
                try
                {
                    var response = await HttpClient.GetAsync(
                        UrlHelper.BuildRelativeUrl("api/note/count"));

                    if (response.IsSuccessStatusCode)
                    {
                        var countJson = await response.Content.ReadAsStringAsync();

                        using var doc = JsonDocument.Parse(countJson);

                        if (doc.RootElement.TryGetProperty("count", out var countElement))
                        {
                            var serverCount = countElement.GetInt32();

                            if (serverCount >= 5)
                                return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NoteApiClient] Note count check failed: {ex}");
                }
            }

            notesDict[userId] = noteText;

            var newJson = JsonSerializer.Serialize(root);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            await File.WriteAllTextAsync(localPath, newJson);

            try
            {
                var requestBody = new
                {
                    UserId = userId,
                    NoteText = noteText
                };

                var serverJson = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(serverJson, Encoding.UTF8, "application/json");

                await HttpClient.PostAsync(
                    UrlHelper.BuildRelativeUrl("api/note/create"),
                    content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoteApiClient] Push note to server failed: {ex}");
            }

            return true;
        }
        catch
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
                var root = DeserializeLocalNotes(json);

                var creatorKey = GetCreatorKey();

                if (!string.IsNullOrEmpty(creatorKey) &&
                    root.TryGetValue(creatorKey, out var notesDict))
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
                await HttpClient.DeleteAsync(
                    UrlHelper.BuildRelativeUrl($"api/user/{userId}/note"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoteApiClient] Delete note request failed: {ex}");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<int> GetNotesCountAsync()
    {
        try
        {
            var localPath = GetLocalNotesPath();

            if (!File.Exists(localPath))
                return Task.FromResult(0);

            var json = File.ReadAllText(localPath);
            var root = DeserializeLocalNotes(json);

            var creatorKey = GetCreatorKey();

            if (!string.IsNullOrEmpty(creatorKey) &&
                root.TryGetValue(creatorKey, out var notesDict))
            {
                return Task.FromResult(notesDict.Count);
            }

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NoteApiClient] GetNotesCountAsync failed: {ex}");
            return Task.FromResult(0);
        }
    }

    private string GetCreatorKey()
    {
        return _creatorKeyProvider();
    }

    private static string GetLocalNotesPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Edemly", "notes", "contact_notes.json");
    }

    private static Dictionary<string, Dictionary<int, string>> DeserializeLocalNotes(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, Dictionary<int, string>>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(
                       json,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       })
                   ?? new Dictionary<string, Dictionary<int, string>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<int, string>>();
        }
    }
}