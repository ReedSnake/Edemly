#nullable disable

using System.Windows.Controls;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public class MessageRenderer
    {
        private StackPanel _messagesPanel;
        private readonly int _currentUserId;
        private bool _isGroupChat;

        private readonly MessageUiUpdater _uiUpdater;
        private readonly TextMessageRenderer _textRenderer;
        private readonly PhotoMessageRenderer _photoRenderer;
        private readonly FileMessageRenderer _fileRenderer;
        private readonly VoiceMessageRenderer _voiceRenderer;

        public MessageRenderer(StackPanel messagesPanel, int currentUserId)
        {
            _messagesPanel = messagesPanel;
            _currentUserId = currentUserId;

            var themeProvider = new MessageThemeProvider();
            var timeFormatter = new MessageTimeFormatter();
            var bubbleFactory = new MessageBubbleFactory();
            _uiUpdater = new MessageUiUpdater(messagesPanel);

            IMessageEditDialogService editDialogService = new MessageEditDialogService();
            var actions = new MessageActions(_uiUpdater, editDialogService);
            IMessageContextMenuFactory contextMenuFactory = new MessageContextMenuFactory(actions);

            _textRenderer = new TextMessageRenderer(themeProvider, timeFormatter, bubbleFactory, contextMenuFactory);
            _photoRenderer = new PhotoMessageRenderer(themeProvider, timeFormatter, bubbleFactory, contextMenuFactory, actions);
            _fileRenderer = new FileMessageRenderer(themeProvider, timeFormatter, bubbleFactory, contextMenuFactory, actions);
            _voiceRenderer = new VoiceMessageRenderer(contextMenuFactory);
        }

        public void SetGroupChatMode(bool isGroupChat)
        {
            _isGroupChat = isGroupChat;
        }

        public void RenderMessage(MessageDto message, bool isHistorical = false, string senderName = null)
        {
            var context = new MessageRenderContext(
                _messagesPanel,
                _currentUserId,
                _isGroupChat,
                senderName ?? string.Empty);

            switch (message.Type)
            {
                case 0:
                    _textRenderer.Render(message, context, isHistorical);
                    break;
                case 1:
                    _voiceRenderer.Render(message, context, isHistorical);
                    break;
                case 3:
                    _photoRenderer.Render(message, context, isHistorical);
                    break;
                case 4:
                case 5:
                    _fileRenderer.Render(message, context, isHistorical);
                    break;
            }
        }

        public void UpdateMessageInUI(MessageDto updatedMessage)
        {
            _uiUpdater.UpdateMessageInUI(updatedMessage, _currentUserId);
        }

        public void RemoveMessageFromUI(int messageId)
        {
            _uiUpdater.RemoveMessageFromUI(messageId);
        }

        public void UpdateMessagesPanel(StackPanel messagesPanel)
        {
            _messagesPanel = messagesPanel;
            _uiUpdater.UpdateMessagesPanel(messagesPanel);
        }
    }
}
