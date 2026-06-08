#nullable disable

using Edemly.Contracts.Messages;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageRenderContext
    {
        public MessageRenderContext(
            StackPanel messagesPanel,
            int currentUserId,
            bool isGroupChat,
            string senderName)
        {
            MessagesPanel = messagesPanel;
            CurrentUserId = currentUserId;
            IsGroupChat = isGroupChat;
            SenderName = senderName;
        }

        public StackPanel MessagesPanel { get; }
        public int CurrentUserId { get; }
        public bool IsGroupChat { get; }
        public string SenderName { get; }

        public bool IsMine(MessageDto message) => message.SenderId == CurrentUserId;
    }
}
