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
        var result = await GetAsync<ContactNoteResponseDto>($"api/users/{userId}/note");
        return result?.Note?.Content;
    }

    public async Task<bool> SaveContactNoteAsync(int userId, string noteText)
    {
        var request = new SaveContactNoteDto
        {
            Content = noteText
        };

        var result = await PutAsync($"api/users/{userId}/note", request);
        return result.Success;
    }

    public Task<bool> DeleteContactNoteAsync(int userId)
    {
        return DeleteAsync($"api/users/{userId}/note");
    }

    public async Task<int> GetNotesCountAsync()
    {
        var result = await GetAsync<NoteCountResponseDto>("api/notes/count");
        return result?.Count ?? 0;
    }

    private sealed class ContactNoteResponseDto
    {
        public NoteDto? Note { get; set; }
    }
}