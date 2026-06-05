#nullable disable

using Edemly.Client.Presentation.Rendering.Common;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class TextMessageRenderer
    {
        private readonly MessageThemeProvider _themeProvider;
        private readonly MessageTimeFormatter _timeFormatter;
        private readonly MessageBubbleFactory _bubbleFactory;
        private readonly MessageContextMenuFactory _contextMenuFactory;

        public TextMessageRenderer(
            MessageThemeProvider themeProvider,
            MessageTimeFormatter timeFormatter,
            MessageBubbleFactory bubbleFactory,
            MessageContextMenuFactory contextMenuFactory)
        {
            _themeProvider = themeProvider;
            _timeFormatter = timeFormatter;
            _bubbleFactory = bubbleFactory;
            _contextMenuFactory = contextMenuFactory;
        }

        public void Render(MessageDto message, MessageRenderContext context, bool isHistorical)
        {
            bool isMine = context.IsMine(message);

            var messageContainer = new StackPanel
            {
                Margin = new Thickness(12, 8, 12, 8)
            };

            if (!isMine && context.IsGroupChat && !string.IsNullOrEmpty(context.SenderName))
            {
                var senderNameText = new TextBlock
                {
                    Text = context.SenderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _themeProvider.GetGroupSenderBrush(),
                    Margin = new Thickness(0, 0, 0, 3)
                };

                messageContainer.Children.Add(senderNameText);
            }

            var messageText = RichTextHelper.CreateRichTextBlock(
                message.Text,
                isMine ? _themeProvider.GetMyTextBrush() : _themeProvider.GetFriendTextBrush(),
                allowSelection: true);
            messageText.Margin = new Thickness(0, 0, 0, 5);

            var timeText = new TextBlock
            {
                Text = _timeFormatter.Format(message.SentAt, isHistorical),
                FontSize = 10,
                Foreground = isMine ? _themeProvider.GetMyTextBrush() : _themeProvider.GetFriendTextBrush(),
                Opacity = 0,
                HorizontalAlignment = isMine ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isMine)
            {
                var bottomPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                bottomPanel.Children.Add(timeText);
                messageContainer.Children.Add(messageText);
                messageContainer.Children.Add(bottomPanel);
            }
            else
            {
                messageContainer.Children.Add(messageText);
                messageContainer.Children.Add(timeText);
            }

            var messageBorder = _bubbleFactory.CreateBubble(
                message.Id,
                isMine ? _themeProvider.GetMyBubbleBrush() : _themeProvider.GetFriendBubbleBrush(),
                isMine ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                isMine ? new Thickness(150, 8, 15, 8) : new Thickness(15, 8, 150, 8),
                isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                500,
                new Thickness(0),
                isHistorical);

            messageBorder.Child = messageContainer;
            _bubbleFactory.AttachTimeHover(messageBorder, timeText);
            _contextMenuFactory.Attach(messageBorder, message, context);
            _bubbleFactory.AddToPanel(context.MessagesPanel, messageBorder, isHistorical);
        }
    }
}
