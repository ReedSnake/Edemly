#nullable enable

namespace Edemly.Client.Presentation.Controllers.Chats
{
    public partial class ChatWorkspaceController
    {
        public bool IsCurrentChatGroup()
        {
            if (CurrentChatId < 0)
            {
                return false;
            }

            return _chatTypes.TryGetValue(CurrentChatId, out var chatType) && chatType == 1;
        }

        public void UpdateGroupIcon(int chatId, string newIconUrl)
        {
            try
            {
                if (!_groupContacts.TryGetValue(chatId, out var contact))
                {
                    System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateGroupIcon: contact not found for chatId {chatId}");
                    return;
                }

                if (!string.IsNullOrEmpty(contact.PhotoPath))
                {
                    try
                    {
                        App.GlobalProfilePictureCache.InvalidateCache(contact.PhotoPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] InvalidateCache failed: {ex}");
                    }
                }

                contact.PhotoPath = newIconUrl;
                UpdateChatButton(chatId);
                TryUpdateCurrentChatMetadata(chatId, photoPath: newIconUrl);

                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] Updated group icon for chat {chatId}: {newIconUrl}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CHAT MANAGER] UpdateGroupIcon error: {ex.Message}");
            }
        }
    }
}
