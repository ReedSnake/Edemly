#nullable disable

using Edemly.Client.Application.Services;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageThemeProvider
    {
        public Brush GetMyTextBrush()
        {
            return new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().TextPrimary);
        }

        public Brush GetFriendTextBrush()
        {
            return Brushes.White;
        }

        public Brush GetMyBubbleBrush()
        {
            return new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().BorderLight);
        }

        public Brush GetFriendBubbleBrush()
        {
            return new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().Primary);
        }

        public Brush GetFileBubbleBrush()
        {
            return new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().Secondary);
        }

        public Brush GetMyFileHoverBrush()
        {
            return new SolidColorBrush(Color.FromArgb(255, 8, 138, 138));
        }

        public Brush GetFriendFileHoverBrush()
        {
            return new SolidColorBrush(Color.FromArgb(255, 7, 150, 150));
        }

        public Brush GetGroupSenderBrush()
        {
            return new SolidColorBrush(Color.FromRgb(180, 220, 220));
        }
    }
}
