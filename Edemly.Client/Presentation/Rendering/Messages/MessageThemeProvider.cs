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
            return ResolveBrush("ThemeOnPrimaryTextBrush", Colors.White);
        }

        public Brush GetColoredBubbleTextBrush()
        {
            return ResolveBrush("ThemeOnSecondaryTextBrush", Colors.White);
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
            var palette = ThemeService.Instance.GetCurrentPalette();
            return new SolidColorBrush(Blend(palette.Secondary, palette.PrimaryLight, 0.18));
        }

        public Brush GetFriendFileHoverBrush()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            return new SolidColorBrush(Blend(palette.Primary, palette.PrimaryLight, 0.24));
        }

        public Brush GetGroupSenderBrush()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            return new SolidColorBrush(Blend(palette.BorderLight, Colors.White, 0.24));
        }

        private static Brush ResolveBrush(string resourceKey, Color fallbackColor)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallbackColor);
        }

        private static Color Blend(Color first, Color second, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));

            return Color.FromRgb(
                (byte)Math.Round(first.R + ((second.R - first.R) * amount)),
                (byte)Math.Round(first.G + ((second.G - first.G) * amount)),
                (byte)Math.Round(first.B + ((second.B - first.B) * amount)));
        }
    }
}
