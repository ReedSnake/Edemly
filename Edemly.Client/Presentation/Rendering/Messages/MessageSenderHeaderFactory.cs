#nullable disable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    internal static class MessageSenderHeaderFactory
    {
        internal static TextBlock Create(MessageRenderContext context, bool isMine, Brush foreground, Thickness margin)
        {
            if (isMine || !context.IsGroupChat || string.IsNullOrWhiteSpace(context.SenderName))
            {
                return null;
            }

            return new TextBlock
            {
                Text = context.SenderName,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = foreground,
                Margin = margin
            };
        }
    }
}
