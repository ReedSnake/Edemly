#nullable enable

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Pages.Main
{
    internal static class PageMainAvatarHelper
    {
        internal static bool HasCustomAvatar(string? photoPath)
        {
            return !string.IsNullOrWhiteSpace(photoPath)
                && !string.Equals(photoPath, Models.Contact.DefaultAvatarPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static ImageBrush CreateAvatarBrush()
        {
            return new ImageBrush
            {
                Stretch = Stretch.UniformToFill,
                ImageSource = CreateDefaultAvatar()
            };
        }

        internal static void ApplyDefaultAvatar(ImageBrush imageBrush)
        {
            ApplyImageSource(imageBrush, CreateDefaultAvatar());
        }

        internal static void ApplyImageSource(ImageBrush imageBrush, ImageSource? imageSource)
        {
            imageBrush.ImageSource = imageSource ?? CreateDefaultAvatar();
        }

        internal static async Task<ImageSource> ResolveImageSourceAsync(string? photoPath, string tracePrefix)
        {
            try
            {
                if (HasCustomAvatar(photoPath))
                {
                    System.Diagnostics.Debug.WriteLine($"{tracePrefix} Loading avatar from: {photoPath}");

                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(photoPath!);
                    if (bitmap != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"{tracePrefix} Avatar loaded successfully");
                        return bitmap;
                    }

                    System.Diagnostics.Debug.WriteLine($"{tracePrefix} Avatar unavailable, using default");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"{tracePrefix} Using default avatar");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{tracePrefix} Avatar load failed: {ex.Message}");
            }

            return CreateDefaultAvatar();
        }

        internal static async Task SetImageSourceAsync(ImageBrush imageBrush, string? photoPath, string tracePrefix)
        {
            var imageSource = await ResolveImageSourceAsync(photoPath, tracePrefix);
            ApplyImageSource(imageBrush, imageSource);
        }

        private static BitmapImage CreateDefaultAvatar()
        {
            return new BitmapImage(new Uri(Models.Contact.DefaultAvatarPath, UriKind.RelativeOrAbsolute));
        }
    }
}
