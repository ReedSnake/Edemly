#nullable disable

using Edemly.Client.Application.Localization;
using System.IO;
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
        private readonly MessageContextMenuFactory _contextMenuFactory;
        private readonly MessageActions _actions;

        public FileMessageRenderer(
            MessageThemeProvider themeProvider,
            MessageTimeFormatter timeFormatter,
            MessageBubbleFactory bubbleFactory,
            MessageContextMenuFactory contextMenuFactory,
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

            if (!isMine && context.IsGroupChat && !string.IsNullOrEmpty(context.SenderName))
            {
                var senderNameText = new TextBlock
                {
                    Text = context.SenderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _themeProvider.GetGroupSenderBrush(),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(senderNameText);
            }

            var fileInfoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var fileIcon = new TextBlock
            {
                Text = GetFileIcon(message.FileName),
                FontSize = 20,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var fileName = new TextBlock
            {
                Text = message.FileName ?? "File",
                FontSize = 14,
                Foreground = Brushes.White,
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
                Foreground = Brushes.White,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var timeText = new TextBlock
            {
                Text = _timeFormatter.Format(message.SentAt, isHistorical),
                FontSize = 10,
                Foreground = Brushes.White,
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

        private static string GetFileIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return "\U0001F4C1";
            }

            var extension = Path.GetExtension(fileName).ToLower();

            return extension switch
            {
                ".pdf" => "\U0001F4C4",
                ".doc" or ".docx" => "\U0001F4DD",
                ".xls" or ".xlsx" => "\U0001F4CA",
                ".ppt" or ".pptx" => "\U0001F4C8",
                ".txt" => "\U0001F4C4",
                ".zip" or ".rar" or ".7z" => "\U0001F5DC\uFE0F",
                ".mp3" or ".wav" or ".flac" => "\U0001F3B5",
                ".mp4" or ".avi" or ".mkv" => "\U0001F3AC",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "\U0001F5BC\uFE0F",
                _ => "\U0001F4C1"
            };
        }
    }
}
