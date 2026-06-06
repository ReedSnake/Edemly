#nullable enable

using System.Windows;

namespace Edemly.Client.Pages.Main
{
    public partial class Page_main
    {
        private const int MAX_CONTACTS_WITH_NOTES = 5;

        private async Task LoadContactNotesAsync(Models.Contact contact, int requestId)
        {
            var notesService = await WaitForNotesServiceAsync(maxRetries: 5, delayMs: 75);
            if (!IsContactInfoRequestCurrent(requestId, contact.UserId))
            {
                return;
            }

            if (notesService == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONTACT NOTES] Notes service unavailable during contact info refresh");
                return;
            }

            try
            {
                var note = await notesService.GetNoteAsync(contact.UserId) ?? string.Empty;
                if (!IsContactInfoRequestCurrent(requestId, contact.UserId))
                {
                    return;
                }

                contact.Note = note;
                _chatController?.TrySetCurrentChatNote(contact.UserId, note);
                ApplyContactNoteState(note);

                if (string.IsNullOrWhiteSpace(note))
                {
                    await ApplyContactNoteAvailabilityAsync(notesService, contact.UserId, requestId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT NOTES] Error loading contact note: {ex.Message}");

                if (!IsContactInfoRequestCurrent(requestId, contact.UserId))
                {
                    return;
                }

                NoNotesText.Text = DefaultLanguage.Error;
            }
        }

        private async void SaveContactNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatController?.CurrentChatContact == null)
            {
                return;
            }

            var noteText = ContactNoteEditor.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(noteText))
            {
                MessageBox.ShowWarning(DefaultLanguage.ContactEmptyNoteWarning, DefaultLanguage.ContactWarningTitle);
                return;
            }

            var notesService = await WaitForNotesServiceAsync();
            if (notesService == null)
            {
                MessageBox.ShowWarning(DefaultLanguage.ContactNotesServiceError, DefaultLanguage.ContactErrorTitle);
                return;
            }

            try
            {
                var contact = _chatController.CurrentChatContact;
                var hasExistingNote = !string.IsNullOrWhiteSpace(contact.Note);

                if (!hasExistingNote)
                {
                    var canAdd = await notesService.CanAddNoteAsync(contact.UserId);
                    if (!canAdd)
                    {
                        MessageBox.ShowWarning(
                            string.Format(DefaultLanguage.ContactNotesLimitReached, MAX_CONTACTS_WITH_NOTES),
                            DefaultLanguage.ContactWarningTitle);
                        await ApplyContactNoteAvailabilityAsync(notesService, contact.UserId);
                        return;
                    }
                }

                var success = await notesService.SaveNoteAsync(contact.UserId, noteText);
                if (!success)
                {
                    MessageBox.ShowError(DefaultLanguage.ContactSaveNoteError, DefaultLanguage.ContactErrorTitle);
                    return;
                }

                contact.Note = noteText;
                _chatController?.TrySetCurrentChatNote(contact.UserId, noteText);
                ApplyContactNoteState(noteText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT NOTES] Save note error: {ex.Message}");
                MessageBox.ShowError(
                    string.Format(DefaultLanguage.ContactSaveNoteErrorDetails, ex.Message),
                    DefaultLanguage.ContactErrorTitle);
            }
        }

        private async void DeleteContactNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_chatController?.CurrentChatContact == null)
            {
                return;
            }

            var result = MessageBox.ShowQuestion(
                DefaultLanguage.ContactDeleteConfirmMessage,
                DefaultLanguage.ContactDeleteConfirmTitle);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var notesService = await WaitForNotesServiceAsync();
            if (notesService == null)
            {
                MessageBox.ShowWarning(DefaultLanguage.ContactNotesServiceError, DefaultLanguage.ContactErrorTitle);
                return;
            }

            try
            {
                var contact = _chatController.CurrentChatContact;
                var success = await notesService.DeleteNoteAsync(contact.UserId);
                if (!success)
                {
                    MessageBox.ShowError(DefaultLanguage.ContactDeleteNoteError, DefaultLanguage.ContactErrorTitle);
                    return;
                }

                contact.Note = string.Empty;
                _chatController?.TrySetCurrentChatNote(contact.UserId, string.Empty);
                ApplyContactNoteState(string.Empty);
                await ApplyContactNoteAvailabilityAsync(notesService, contact.UserId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT NOTES] Delete note error: {ex.Message}");
                MessageBox.ShowError(
                    string.Format(DefaultLanguage.ContactDeleteNoteErrorDetails, ex.Message),
                    DefaultLanguage.ContactErrorTitle);
            }
        }

        private async Task<Edemly.Client.Application.Services.NotesService?> WaitForNotesServiceAsync(int maxRetries = 10, int delayMs = 100)
        {
            var retries = 0;
            while (App.NotesService == null && retries < maxRetries)
            {
                await Task.Delay(delayMs);
                retries++;
            }

            return App.NotesService;
        }
    }
}
