using Edemly.Client.Api.Core;
using Edemly.Contracts.Notes;

namespace Edemly.Client.Api.Notes;

public sealed class NoteApiClient : ApiClientBase, INoteApiClient
{
    public NoteApiClient(ApiClientContext context)
        : base(context)
    {
    }

    public async Task<string?> GetContactNoteAsync(int userId)
    {
        var result = await GetAsync<NoteResponseDto>($"api/user/{userId}/note");
        return result?.Note;
    }

    public async Task<bool> SaveContactNoteAsync(int userId, string noteText)
    {
        var request = new SaveContactNoteRequestDto
        {
            UserId = userId,
            NoteText = noteText
        };

        var result = await PostAsync<SaveContactNoteRequestDto, object>("api/note/create", request);
        return result != null;
    }

    public Task<bool> DeleteContactNoteAsync(int userId)
    {
        return DeleteAsync($"api/user/{userId}/note");
    }

    public async Task<int> GetNotesCountAsync()
    {
        var result = await GetAsync<NoteCountResponseDto>("api/note/count");
        return result?.Count ?? 0;
    }
}