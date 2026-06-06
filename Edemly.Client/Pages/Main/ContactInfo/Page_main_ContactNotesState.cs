#nullable enable

using System.Windows;

namespace Edemly.Client.Pages.Main
{
    public partial class Page_main
    {
        private void PrepareContactNotesForDisplay(string? note)
        {
            NoNotesText.Text = DefaultLanguage.NoNotes;
            ContactNoteLimitWarning.Visibility = Visibility.Collapsed;
            ContactNoteLimitWarning.Text = string.Empty;
            ContactNoteEditor.IsEnabled = true;
            SaveContactNoteButton.IsEnabled = true;

            ApplyContactNoteState(note ?? string.Empty);
        }

        private async Task ApplyContactNoteAvailabilityAsync(Edemly.Client.Application.Services.NotesService notesService, int userId, int? requestId = null)
        {
            var canAdd = await notesService.CanAddNoteAsync(userId);

            if (requestId.HasValue && !IsContactInfoRequestCurrent(requestId.Value, userId))
            {
                return;
            }

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
