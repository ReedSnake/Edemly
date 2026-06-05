#nullable disable

using Edemly.Client.Presentation.Rendering.Common;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class PhotoMessageRenderer
    {
        private readonly MessageThemeProvider _themeProvider;
        private readonly MessageTimeFormatter _timeFormatter;
        private readonly MessageBubbleFactory _bubbleFactory;
        private readonly MessageContextMenuFactory _contextMenuFactory;
        private readonly MessageActions _actions;

        public PhotoMessageRenderer(
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
                isMine ? _themeProvider.GetMyBubbleBrush() : _themeProvider.GetFriendBubbleBrush(),
                isMine ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                isMine ? new Thickness(150, 8, 15, 8) : new Thickness(15, 8, 150, 8),
                isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                400,
                new Thickness(8),
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

            var image = new Image
            {
                MaxWidth = 350,
                MaxHeight = 350,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 5)
            };

            LoadPhotoAsync(message.ContentUrl, image);
            stackPanel.Children.Add(image);

            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                var messageText = RichTextHelper.CreateRichTextBlock(
                    message.Text,
                    isMine ? _themeProvider.GetMyTextBrush() : _themeProvider.GetFriendTextBrush(),
                    allowSelection: true);
                messageText.Margin = new Thickness(0, 5, 0, 5);
                stackPanel.Children.Add(messageText);
            }

            var timeText = new TextBlock
            {
                Text = _timeFormatter.Format(message.SentAt, isHistorical),
                FontSize = 10,
                Foreground = isMine ? _themeProvider.GetMyTextBrush() : _themeProvider.GetFriendTextBrush(),
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            _bubbleFactory.AttachTimeHover(messageBorder, timeText);
            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                await _actions.OpenDownloadedContentAsync(message.ContentUrl, message.FileName ?? "image.jpg");
            };

            _contextMenuFactory.Attach(messageBorder, message, context);
            _bubbleFactory.AddToPanel(context.MessagesPanel, messageBorder, isHistorical);
        }

        private static async void LoadPhotoAsync(string url, Image imageControl)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    return;
                }

                var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(url);
                if (bitmap != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        imageControl.Source = bitmap;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load photo: {ex.Message}");
            }
        }
    }
}
