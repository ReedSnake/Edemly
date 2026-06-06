#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Attachments;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class FileMessageRenderer
    {
        private readonly MessageThemeProvider _themeProvider;
        private readonly MessageTimeFormatter _timeFormatter;
        private readonly MessageBubbleFactory _bubbleFactory;
        private readonly IMessageContextMenuFactory _contextMenuFactory;
        private readonly MessageActions _actions;

        public FileMessageRenderer(
            MessageThemeProvider themeProvider,
            MessageTimeFormatter timeFormatter,
            MessageBubbleFactory bubbleFactory,
            IMessageContextMenuFactory contextMenuFactory,
            MessageActions actions)
        {
            _themeProvider = themeProvider;
            _timeFormatter = timeFormatter;
            _bubbleFactory = bubbleFactory;
            _contextMenuFactory = contextMenuFactory;
            _actions = actions;
        }

        public void Render(MessageDto message, MessageRenderContext context, bool isHistorical)
        {
            bool isMine = context.IsMine(message);
            var bubbleTextBrush = _themeProvider.GetColoredBubbleTextBrush();

            var messageBorder = _bubbleFactory.CreateBubble(
                message.Id,
                isMine ? _themeProvider.GetFileBubbleBrush() : _themeProvider.GetFriendBubbleBrush(),
                isMine ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                isMine ? new Thickness(150, 8, 15, 8) : new Thickness(15, 8, 150, 8),
                isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                400,
                new Thickness(12, 10, 12, 10),
                isHistorical,
                Cursors.Hand);

            var stackPanel = new StackPanel();

            var senderNameText = MessageSenderHeaderFactory.Create(
                context,
                isMine,
                _themeProvider.GetGroupSenderBrush(),
                new Thickness(0, 0, 0, 5));

            if (senderNameText != null)
            {
                stackPanel.Children.Add(senderNameText);
            }

            var fileInfoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var fileIcon = new TextBlock
            {
                Text = AttachmentFileIconResolver.GetIconGlyph(message.FileName),
                FontSize = 20,
                Foreground = bubbleTextBrush,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var fileName = new TextBlock
            {
                Text = message.FileName ?? "File",
                FontSize = 14,
                Foreground = bubbleTextBrush,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            fileInfoPanel.Children.Add(fileIcon);
            fileInfoPanel.Children.Add(fileName);

            var hintText = new TextBlock
            {
                Text = DefaultLanguage.ClickToOpen,
                FontSize = 11,
                Foreground = bubbleTextBrush,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var timeText = new TextBlock
            {
                Text = _timeFormatter.Format(message.SentAt, isHistorical),
                FontSize = 10,
                Foreground = bubbleTextBrush,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Add(fileInfoPanel);
            stackPanel.Children.Add(hintText);
            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            messageBorder.MouseEnter += (s, e) =>
            {
                messageBorder.Background = isMine
                    ? _themeProvider.GetMyFileHoverBrush()
                    : _themeProvider.GetFriendFileHoverBrush();
                timeText.Opacity = 0.7;
            };

            messageBorder.MouseLeave += (s, e) =>
            {
                messageBorder.Background = isMine
                    ? _themeProvider.GetFileBubbleBrush()
                    : _themeProvider.GetFriendBubbleBrush();
                timeText.Opacity = 0;
            };

            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                await _actions.OpenDownloadedContentAsync(message.ContentUrl, message.FileName ?? "file");
            };

            _contextMenuFactory.Attach(messageBorder, message, context);
            _bubbleFactory.AddToPanel(context.MessagesPanel, messageBorder, isHistorical);
        }
    }
}
