#nullable enable

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        private void NotifyCurrentChatHeader()
        {
            if (CurrentChatContact == null)
            {
                _updateChatHeaderCallback?.Invoke(null);
                return;
            }

            _chatHeaderText.Text = CurrentChatContact.Name;
            _updateChatHeaderCallback?.Invoke(CurrentChatContact);
        }

        public bool TryUpdateCurrentChatMetadata(int chatId, string? name = null, string? photoPath = null)
        {
            if (CurrentChatId != chatId || CurrentChatContact == null)
            {
                return false;
            }

            var changed = false;

            if (!string.IsNullOrEmpty(name) && CurrentChatContact.Name != name)
            {
                CurrentChatContact.Name = name;
                changed = true;
            }

            if (!string.IsNullOrEmpty(photoPath) && CurrentChatContact.PhotoPath != photoPath)
            {
                CurrentChatContact.PhotoPath = photoPath;
                changed = true;
            }

            if (changed)
            {
                NotifyCurrentChatHeader();
            }

            return changed;
        }

        public bool TryUpdateCurrentChatPhotoForUser(int userId, string photoPath)
        {
            if (CurrentChatContact?.UserId != userId)
            {
                return false;
            }

            if (CurrentChatContact.PhotoPath == photoPath)
            {
                return false;
            }

            CurrentChatContact.PhotoPath = photoPath;
            NotifyCurrentChatHeader();
            return true;
        }

        public bool TrySetCurrentChatNote(int userId, string note)
        {
            if (CurrentChatContact?.UserId != userId)
            {
                return false;
            }

            CurrentChatContact.Note = note;
            return true;
        }

        public void UpdateChatButtonName(int chatId, string newName)
        {
            if (!_chatListItemStateFactory.TryGetContact(chatId, out var contact) || contact == null)
            {
                return;
            }

            contact.Name = newName;
            UpdateChatButton(chatId);
            TryUpdateCurrentChatMetadata(chatId, name: newName);
        }
    }
}
