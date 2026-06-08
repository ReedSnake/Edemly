#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Application.Theme;
using Edemly.Contracts.Messages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public static partial class VoiceMessageHelper
    {
        public static void AddMyVoiceMessage(
            MessageDto message,
            MessageRenderContext context,
            bool isHistorical,
            IMessageContextMenuFactory contextMenuFactory)
        {
            var border = BuildVoiceMessageBorder(
                message,
                isMine: true,
                context,
                isHistorical,
                senderName: null,
                contextMenuFactory);

            context.MessagesPanel.Children.Add(border);

            if (!isHistorical)
            {
                AnimateFadeIn(border);
            }
        }

        public static void AddFriendVoiceMessage(
            MessageDto message,
            MessageRenderContext context,
            bool isHistorical,
            string senderName,
            IMessageContextMenuFactory contextMenuFactory)
        {
            var border = BuildVoiceMessageBorder(
                message,
                isMine: false,
                context,
                isHistorical,
                senderName,
                contextMenuFactory);

            context.MessagesPanel.Children.Add(border);

            if (!isHistorical)
            {
                AnimateFadeIn(border);
            }
        }

        private static Border BuildVoiceMessageBorder(
            MessageDto message,
            bool isMine,
            MessageRenderContext context,
            bool isHistorical,
            string senderName,
            IMessageContextMenuFactory contextMenuFactory)
        {
            var palette = ThemeService.Instance.GetCurrentPalette();

            var bg = isMine ? palette.BorderLight : palette.Primary;
            var playBtnBg = isMine ? palette.Primary : palette.BorderLight;
            var playBtnFg = isMine ? Brushes.White : new SolidColorBrush(palette.Primary);
            var progressColor = isMine ? new SolidColorBrush(palette.Primary) : Brushes.White;
            var textColor = isMine ? new SolidColorBrush(palette.TextPrimary) : Brushes.White;

            var messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(bg),
                CornerRadius = isMine ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                Margin = isMine ? new Thickness(150, 8, 15, 8) : new Thickness(15, 8, 150, 8),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 300,
                Opacity = isHistorical ? 0.8 : 1
            };

            var mainPanel = new StackPanel();

            var senderNameText = MessageSenderHeaderFactory.Create(
                context,
                isMine,
                new MessageThemeProvider().GetGroupSenderBrush(),
                new Thickness(0, 0, 0, 5));

            if (senderNameText != null)
            {
                mainPanel.Children.Add(senderNameText);
            }

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var playButton = CreateCircularButton(playBtnBg, playBtnFg);
            var positionSlider = CreateCustomSlider();
            ApplySliderColors(positionSlider, progressColor);

            var timeText = new TextBlock
            {
                Text = "00:00 / 00:00",
                FontSize = 12,
                Foreground = textColor,
                VerticalAlignment = VerticalAlignment.Center
            };

            playButton.Click += async (s, e) => await HandlePlayPauseAsync(message, playButton, positionSlider, timeText, messageBorder);

            positionSlider.PreviewMouseDown += (s, e) => { _isUserDragging = true; };
            positionSlider.PreviewMouseUp += async (s, e) =>
            {
                _isUserDragging = false;

                if (_currentMessageId == message.Id && _audioFile != null)
                {
                    SeekAudio(positionSlider.Value);
                    return;
                }

                if (_currentMessageId != message.Id)
                {
                    await HandlePlayPauseAsync(message, playButton, positionSlider, timeText, messageBorder, startAtSeconds: positionSlider.Value);
                }
            };

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var voiceLabel = new TextBlock
            {
                Text = DefaultLanguage.VoiceMessage,
                FontSize = 13,
                Foreground = textColor,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3)
            };

            var timeSent = new TextBlock
            {
                Text = isHistorical ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm") : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = textColor,
                Opacity = 0.7
            };

            infoPanel.Children.Add(voiceLabel);
            infoPanel.Children.Add(timeSent);

            var controlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            controlsPanel.Children.Add(playButton);
            controlsPanel.Children.Add(positionSlider);
            controlsPanel.Children.Add(timeText);

            stackPanel.Children.Add(controlsPanel);
            stackPanel.Children.Add(infoPanel);
            mainPanel.Children.Add(stackPanel);
            messageBorder.Child = mainPanel;

            AddVoiceMessageContextMenu(messageBorder, message, context, contextMenuFactory);
            PrefetchDuration(message, positionSlider, timeText);

            return messageBorder;
        }

        private static void AddVoiceMessageContextMenu(
            Border messageBorder,
            MessageDto message,
            MessageRenderContext context,
            IMessageContextMenuFactory contextMenuFactory)
        {
            contextMenuFactory.Attach(messageBorder, message, context, MessageContextMenuOptions.ForVoice());
        }

        private static void AnimateFadeIn(UIElement element)
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
    }
}
