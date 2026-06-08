#nullable enable

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Edemly.Client.Presentation.Pages.Main.Helpers
{
    internal static class MainPageInfoPanelHelper
    {
        private const double HiddenOffset = 400;
        private static readonly Duration SlideDuration = TimeSpan.FromSeconds(0.3);

        internal static void PrepareToShow(UIElement panel, UIElement overlay)
        {
            overlay.Visibility = Visibility.Visible;
            panel.Visibility = Visibility.Visible;
        }

        internal static void SlideIn(TranslateTransform transform)
        {
            Animate(transform, HiddenOffset, 0);
        }

        internal static async Task HideAsync(
            UIElement panel,
            UIElement overlay,
            TranslateTransform transform,
            Dispatcher dispatcher)
        {
            Animate(transform, 0, HiddenOffset);

            await Task.Delay(SlideDuration.TimeSpan);
            await dispatcher.InvokeAsync(() =>
            {
                panel.Visibility = Visibility.Collapsed;
                overlay.Visibility = Visibility.Collapsed;
            });
        }

        private static void Animate(TranslateTransform transform, double from, double to)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = SlideDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
    }
}
