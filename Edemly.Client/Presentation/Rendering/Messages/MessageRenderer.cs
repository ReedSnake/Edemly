#nullable disable

using Edemly.Contracts.Messages;
using System.Diagnostics;
using System.IO;
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
        private readonly CallSystemMessageRenderer _callSystemRenderer;

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
            _callSystemRenderer = new CallSystemMessageRenderer();
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

            if (message.Type != MessageTypeCodes.Photo && IsImageAttachment(message))
            {
                Debug.WriteLine($"[MessageRenderer] Rendering image attachment as photo. Message='{message.Id}', Type='{message.Type}', ContentUrl='{message.ContentUrl}', FileName='{message.FileName}'.");
                _photoRenderer.Render(message, context, isHistorical);
                return;
            }

            switch (message.Type)
            {
                case MessageTypeCodes.Text:
                    _textRenderer.Render(message, context, isHistorical);
                    break;
                case MessageTypeCodes.Voice:
                    _voiceRenderer.Render(message, context, isHistorical);
                    break;
                case MessageTypeCodes.Photo:
                    _photoRenderer.Render(message, context, isHistorical);
                    break;
                case MessageTypeCodes.File:
                case MessageTypeCodes.Document:
                    _fileRenderer.Render(message, context, isHistorical);
                    break;
                case MessageTypeCodes.Call:
                    _callSystemRenderer.Render(message, context, isHistorical);
                    break;
                default:
                    Debug.WriteLine($"[MessageRenderer] Unsupported message type '{message.Type}' for message '{message.Id}'. ContentUrl='{message.ContentUrl}'.");
                    break;
            }
        }

        private static bool IsImageAttachment(MessageDto message)
        {
            return HasImageExtension(message.FileName) || HasImageExtension(message.ContentUrl);
        }

        private static bool HasImageExtension(string value)
        {
            var extension = GetExtension(value);
            return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

        private static string GetExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
                {
                    return Path.GetExtension(absoluteUri.AbsolutePath).ToLowerInvariant();
                }

                var path = value;
                var queryIndex = path.IndexOf('?');
                if (queryIndex >= 0)
                {
                    path = path[..queryIndex];
                }

                return Path.GetExtension(path).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MessageRenderer] Failed to resolve file extension for '{value}': {ex.Message}");
                return string.Empty;
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
