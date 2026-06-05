using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
namespace Edemly.Client.Presentation.Behaviors
{
    public static class SmoothScrollBehavior
    {
        public static readonly DependencyProperty EnableSmoothScrollingProperty =
            DependencyProperty.RegisterAttached(
                "EnableSmoothScrolling",
                typeof(bool),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(false, OnEnableSmoothScrollingChanged));

        public static void SetEnableSmoothScrolling(DependencyObject element, bool value) => element.SetValue(EnableSmoothScrollingProperty, value);

        public static bool GetEnableSmoothScrolling(DependencyObject element) => (bool)element.GetValue(EnableSmoothScrollingProperty);

        public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedVerticalOffset",
                typeof(double),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

        public static void SetAnimatedVerticalOffset(DependencyObject element, double value) => element.SetValue(AnimatedVerticalOffsetProperty, value);

        public static double GetAnimatedVerticalOffset(DependencyObject element) => (double)element.GetValue(AnimatedVerticalOffsetProperty);

        private static void OnEnableSmoothScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                bool enable = (bool)e.NewValue;
                if (enable)
                {
                    sv.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                else
                {
                    sv.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                }
            }
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;

            e.Handled = true;

            double delta = -e.Delta;

            double factor = 3.5; // increase for slower scroll
            double target = sv.VerticalOffset + (delta / factor);

            target = Math.Max(0, Math.Min(sv.ScrollableHeight, target));

            var animation = new DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            sv.BeginAnimation(AnimatedVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                try
                {
                    double offset = (double)e.NewValue;
                    sv.ScrollToVerticalOffset(offset);
                }
                catch { }
            }
        }
    }
}