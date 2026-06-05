using Edemly.Client.Application.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Edemly.Client.Presentation.Common
{
    public class ThemedPage : Page
    {
        private bool _isThemeSubscribed;

        public ThemedPage()
        {
            Loaded += ThemedPage_Loaded;
            Unloaded += ThemedPage_Unloaded;
        }

        private void ThemedPage_Loaded(object sender, RoutedEventArgs e)
        {
            SubscribeThemeChanged();
            ApplyThemeSafely("load");
        }

        private void ThemedPage_Unloaded(object sender, RoutedEventArgs e)
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