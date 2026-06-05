#nullable disable

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageBubbleFactory
    {
        public Border CreateBubble(
            int messageId,
            Brush background,
            CornerRadius cornerRadius,
            Thickness margin,
            HorizontalAlignment horizontalAlignment,
            double maxWidth,
            Thickness padding,
            bool isHistorical,
            Cursor cursor = null)
        {
            return new Border
            {
                Tag = messageId,
                Background = background,
                CornerRadius = cornerRadius,
                Margin = margin,
                HorizontalAlignment = horizontalAlignment,
                MaxWidth = maxWidth,
                Padding = padding,
                Cursor = cursor ?? Cursors.Arrow,
                Opacity = isHistorical ? 0.8 : 1
            };
        }

        public void AttachTimeHover(Border border, UIElement timeElement, double visibleOpacity = 0.7)
        {
            border.MouseEnter += (s, e) => { timeElement.Opacity = visibleOpacity; };
            border.MouseLeave += (s, e) => { timeElement.Opacity = 0; };
        }

        public void AddToPanel(StackPanel panel, Border border, bool isHistorical)
        {
            panel.Children.Add(border);

            if (isHistorical)
            {
                return;
            }

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
    }
}
