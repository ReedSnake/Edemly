#nullable enable

using System.Windows;

namespace Edemly.Client.Pages.Main
{
    public partial class Page_main
    {
        private const int MAX_CONTACTS_WITH_NOTES = 5;

        private async Task LoadContactNotesAsync()
        {
            Note1Border.Visibility = Visibility.Collapsed;
            NoNotesText.Visibility = Visibility.Visible;
            NoNotesText.Text = DefaultLanguage.NoNotes;
            ContactNoteLimitWarning.Visibility = Visibility.Collapsed;
            ContactNoteEditor.IsEnabled = true;
            SaveContactNoteButton.IsEnabled = true;

            if (_chatController?.CurrentChatContact == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONTACT NOTES] CurrentChatContact is null");
                ApplyContactNoteState(string.Empty);
                return;
            }

            var notesService = await WaitForNotesServiceAsync();
            if (notesService == null)
            {
                NoNotesText.Text = DefaultLanguage.ContactNotesServiceError;
                ContactNoteEditor.IsEnabled = false;
                SaveContactNoteButton.IsEnabled = false;
                return;
            }

            try
            {
                var contact = _chatController.CurrentChatContact;
                var note = await notesService.GetNoteAsync(contact.UserId) ?? string.Empty;

                contact.Note = note;
                _chatController.TrySetCurrentChatNote(contact.UserId, note);
                ApplyContactNoteState(note);

                if (string.IsNullOrWhiteSpace(note))
                {
                    await ApplyContactNoteAvailabilityAsync(notesService, contact.UserId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT NOTES] Error loading contact note: {ex.Message}");
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
                _chatController.TrySetCurrentChatNote(contact.UserId, noteText);
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
                _chatController.TrySetCurrentChatNote(contact.UserId, string.Empty);
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

        private async Task<Edemly.Client.Application.Services.NotesService?> WaitForNotesServiceAsync()
        {
            var retries = 0;
            while (App.NotesService == null && retries < 10)
            {
                await Task.Delay(100);
                retries++;
            }

            return App.NotesService;
        }

        private async Task ApplyContactNoteAvailabilityAsync(Edemly.Client.Application.Services.NotesService notesService, int userId)
        {
            var canAdd = await notesService.CanAddNoteAsync(userId);
            ContactNoteEditor.IsEnabled = canAdd;
            SaveContactNoteButton.IsEnabled = canAdd;
            ContactNoteLimitWarning.Visibility = canAdd ? Visibility.Collapsed : Visibility.Visible;
            ContactNoteLimitWarning.Text = canAdd
                ? string.Empty
                : string.Format(DefaultLanguage.ContactNotesLimitWarning, MAX_CONTACTS_WITH_NOTES);
        }

        private void ApplyContactNoteState(string note)
        {
            var hasNote = !string.IsNullOrWhiteSpace(note);

            ContactInfoNote1.Text = note;
            Note1Border.Visibility = hasNote ? Visibility.Visible : Visibility.Collapsed;
            NoNotesText.Visibility = hasNote ? Visibility.Collapsed : Visibility.Visible;
            ContactNoteEditor.Text = note;
            SaveContactNoteButton.Content = hasNote
                ? DefaultLanguage.ContactUpdateNoteButton
                : DefaultLanguage.ContactAddNoteButton;
            DeleteContactNoteButton.Visibility = hasNote ? Visibility.Visible : Visibility.Collapsed;

            if (hasNote)
            {
                ContactNoteLimitWarning.Visibility = Visibility.Collapsed;
            }
        }
    }
}
