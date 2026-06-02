using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Edemly.Client.Helpers
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

        // This attached property holds the animated vertical offset value on the ScrollViewer instance
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

            // Prevent default bubbling
            e.Handled = true;

            // Invert delta to match ScrollToVerticalOffset direction
            double delta = -e.Delta;

            // Make scrolling slower / smoother: divide delta
            double factor = 3.5; // increase for slower scroll
            double target = sv.VerticalOffset + (delta / factor);

            // Clamp
            target = Math.Max(0, Math.Min(sv.ScrollableHeight, target));

            // Animate attached property on the ScrollViewer instance
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
