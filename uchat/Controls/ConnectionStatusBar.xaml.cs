using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using uchat.Lang;
using uchat.Services;

namespace uchat.Controls
{
    public partial class ConnectionStatusBar : UserControl
    {
        public ConnectionStatusBar()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Constructor called");
            InitializeComponent();
            Hide();

            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();
        }

        private void OnThemeChanged()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] OnThemeChanged called");
            }
            catch { }
        }

        public void ShowReconnecting()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowReconnecting called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowReconnecting (UI thread)");
                StatusText.Text = DefaultLanguage.Reconnecting;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81));
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                StatusBorder.Visibility = Visibility.Visible;
                RootControl.Visibility = Visibility.Visible;
            });
        }

        public void ShowConnected()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowConnected called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowConnected (UI thread, hiding status bar)");
                Hide();
            });
        }

        public void ShowDisconnected()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowDisconnected called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowDisconnected (UI thread)");
                StatusText.Text = DefaultLanguage.ConnectionLost; // ✅ ЛОКАЛИЗОВАНО
                StatusText.Foreground = Brushes.White;
                StatusBorder.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                StatusBorder.Visibility = Visibility.Visible;
                RootControl.Visibility = Visibility.Visible;
            });
        }

        public void Hide()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Hide called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Hide (UI thread)");
                StatusBorder.Visibility = Visibility.Collapsed;
                RootControl.Visibility = Visibility.Collapsed;
            });
        }
    }
}