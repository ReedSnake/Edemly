using Edemly.Client.Api.Notes;
namespace Edemly.Client.Application.Services
{
    public class NotesService
    {
        private readonly INoteApiClient _apiClient;
        private readonly Dictionary<int, Dictionary<int, string>> _notesCache;
        private bool _isInitialized;
        private const int MAX_CONTACTS_WITH_NOTES = 5;

        public NotesService(INoteApiClient _apiClient)
        {
            _apiClient = _apiClient;
            _notesCache = new Dictionary<int, Dictionary<int, string>>();
            _isInitialized = false;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notes initialization error: {ex.Message}");
            }
        }

        public async Task<string?> GetNoteAsync(int userId)
        {
            if (App.CurrentUserId.HasValue)
            {
                var creatorId = App.CurrentUserId.Value;

                if (_notesCache.TryGetValue(creatorId, out var dict) && dict.TryGetValue(userId, out var cached))
                    return cached;
            }

            try
            {
                var note = await _apiClient.GetContactNoteAsync(userId);

                if (note != null && App.CurrentUserId.HasValue)
                {
                    var creatorId = App.CurrentUserId.Value;
                    if (!_notesCache.ContainsKey(creatorId))
                        _notesCache[creatorId] = new Dictionary<int, string>();

                    _notesCache[creatorId][userId] = note;
                }

                return note;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get note error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SaveNoteAsync(int userId, string noteText)
        {
            try
            {
                var creatorId = App.CurrentUserId ?? -1;
                if (creatorId < 0) return false;

                if (!_notesCache.ContainsKey(creatorId))
                    _notesCache[creatorId] = new Dictionary<int, string>();

                var dict = _notesCache[creatorId];

                if (!dict.ContainsKey(userId))
                {
                    var cfg = ConfigService.Instance;
                    var isCompany = cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company);

                    if (!isCompany)
                    {
                        var serverCount = await _apiClient.GetNotesCountAsync();
                        if (serverCount >= MAX_CONTACTS_WITH_NOTES)
                        {
                            return false;
                        }
                    }
                }

                var success = await _apiClient.SaveContactNoteAsync(userId, noteText);

                if (success)
                {
                    dict[userId] = noteText;
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save note error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteNoteAsync(int userId)
        {
            try
            {
                var success = await _apiClient.DeleteContactNoteAsync(userId);
                if (success && App.CurrentUserId.HasValue)
                {
                    var creatorId = App.CurrentUserId.Value;
                    if (_notesCache.TryGetValue(creatorId, out var dict))
                    {
                        dict.Remove(userId);
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete note error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetNotesCountAsync()
        {
            try
            {
                if (App.CurrentUserId.HasValue)
                {
                    var creatorId = App.CurrentUserId.Value;
                    if (_notesCache.TryGetValue(creatorId, out var dict))
                        return Math.Max(await _apiClient.GetNotesCountAsync(), dict.Count);
                }

                return await _apiClient.GetNotesCountAsync();
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> CanAddNoteAsync(int userId)
        {
            if (App.CurrentUserId.HasValue)
            {
                var creatorId = App.CurrentUserId.Value;
                if (_notesCache.TryGetValue(creatorId, out var dict) && dict.ContainsKey(userId))
                    return true; // updating existing note

                var cfg = ConfigService.Instance;
                var isCompany = cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company);
                if (isCompany) return true;

                var count = await GetNotesCountAsync();
                return count < MAX_CONTACTS_WITH_NOTES;
            }

            return false;
        }

        public void ClearCache()
        {
            _notesCache.Clear();
            _isInitialized = false;
        }
    }
}