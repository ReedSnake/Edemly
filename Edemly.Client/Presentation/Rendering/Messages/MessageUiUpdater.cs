#nullable disable

using Edemly.Client.Presentation.Rendering.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageUiUpdater
    {
        private StackPanel _messagesPanel;

        public MessageUiUpdater(StackPanel messagesPanel)
        {
            _messagesPanel = messagesPanel;
        }

        public void UpdateMessagesPanel(StackPanel messagesPanel)
        {
            _messagesPanel = messagesPanel;
        }

        public void UpdateMessageInUI(MessageDto updatedMessage, int currentUserId)
        {
            var messageBorder = _messagesPanel.Children
                .OfType<Border>()
                .FirstOrDefault(b =>
                {
                    if (b.Tag == null) return false;
                    if (b.Tag is int i) return i == updatedMessage.Id;
                    return int.TryParse(b.Tag.ToString(), out var parsed) && parsed == updatedMessage.Id;
                });

            if (messageBorder == null)
            {
                return;
            }

            var messageContainer = messageBorder.Child as Panel;
            if (messageContainer == null)
            {
                return;
            }

            TextBlock messageText = null;

            foreach (var tb in messageContainer.Children.OfType<TextBlock>())
            {
                if (tb.Cursor == Cursors.IBeam || tb.TextWrapping == TextWrapping.Wrap || tb.FontSize == 14)
                {
                    messageText = tb;
                    break;
                }
            }

            if (messageText == null)
            {
                return;
            }

            var isMyMessage = updatedMessage.SenderId == currentUserId;
            var newBlock = RichTextHelper.CreateRichTextBlock(
                updatedMessage.Text,
                isMyMessage ? Brushes.Black : Brushes.White,
                allowSelection: true);

            newBlock.Margin = messageText.Margin;

            var parent = messageText.Parent as Panel;
            if (parent == null)
            {
                return;
            }

            int index = parent.Children.IndexOf(messageText);
            if (index < 0)
            {
                return;
            }

            parent.Children.RemoveAt(index);
            parent.Children.Insert(index, newBlock);
        }

        public void RemoveMessageFromUI(int messageId)
        {
            var messageBorder = _messagesPanel.Children
                .OfType<Border>()
                .FirstOrDefault(b =>
                {
                    if (b.Tag == null) return false;
                    if (b.Tag is int i) return i == messageId;
                    return int.TryParse(b.Tag.ToString(), out var parsed) && parsed == messageId;
                });

            if (messageBorder != null)
            {
                _messagesPanel.Children.Remove(messageBorder);
            }
        }
    }
}
