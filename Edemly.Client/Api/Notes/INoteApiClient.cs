namespace Edemly.Client.Api.Notes;

public interface INoteApiClient
{
    Task<string?> GetContactNoteAsync(int userId);

    Task<bool> SaveContactNoteAsync(int userId, string noteText);

    Task<bool> DeleteContactNoteAsync(int userId);

    Task<int> GetNotesCountAsync();
}