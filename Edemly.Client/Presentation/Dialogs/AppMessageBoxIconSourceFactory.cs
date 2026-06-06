using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;

namespace Edemly.Client.Presentation.Dialogs
{
    internal static class AppMessageBoxIconSourceFactory
    {
        internal static ImageSource Create(MessageBoxImage icon)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    ResolveSystemIcon(icon).Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));

                source.Freeze();
                return source;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP MESSAGE BOX] Failed to create system icon: {ex.Message}");
                return CreateFallbackGlyph(icon);
            }
        }

        private static DrawingIcon ResolveSystemIcon(MessageBoxImage icon)
        {
            return icon switch
            {
                MessageBoxImage.Warning => DrawingSystemIcons.Warning,
                MessageBoxImage.Error => DrawingSystemIcons.Error,
                MessageBoxImage.Question => DrawingSystemIcons.Question,
                _ => DrawingSystemIcons.Information
            };
        }

        private static ImageSource CreateFallbackGlyph(MessageBoxImage icon)
        {
            var glyph = icon switch
            {
                MessageBoxImage.Warning => "!",
                MessageBoxImage.Error => "x",
                MessageBoxImage.Question => "?",
                _ => "i"
            };

            var formattedText = new FormattedText(
                glyph,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                18,
                System.Windows.Media.Brushes.White,
                1.0);

            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawText(formattedText, new System.Windows.Point(2, 0));
            }

            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
    }
}
