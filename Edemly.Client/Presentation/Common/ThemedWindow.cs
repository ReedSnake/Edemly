using Edemly.Client.Application.Theme;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Presentation.Common
{
    public class ThemedWindow : Window
    {
        private bool _isThemeSubscribed;

        public ThemedWindow()
        {
            Loaded += ThemedWindow_Loaded;
            Closed += ThemedWindow_Closed;
        }

        private void ThemedWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SubscribeThemeChanged();
            ApplyThemeSafely("load");
        }

        private void ThemedWindow_Closed(object? sender, EventArgs e)
        {
            UnsubscribeThemeChanged();
        }

        private void SubscribeThemeChanged()
        {
            if (_isThemeSubscribed)
                return;

            ThemeService.Instance.ThemeChanged += OnThemeChanged;
            _isThemeSubscribed = true;
        }

        private void UnsubscribeThemeChanged()
        {
            if (!_isThemeSubscribed)
                return;

            ThemeService.Instance.ThemeChanged -= OnThemeChanged;
            _isThemeSubscribed = false;
        }

        private void OnThemeChanged(string themeName)
        {
            ApplyThemeSafely($"theme changed to {themeName}");
        }

        private void ApplyThemeSafely(string reason)
        {
            try
            {
                ApplyTheme();
                Debug.WriteLine($"[{GetType().Name}] Theme applied on {reason}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{GetType().Name}] ApplyTheme failed on {reason}: {ex}");
            }
        }

        protected virtual void ApplyTheme()
        {
        }
    }
}
