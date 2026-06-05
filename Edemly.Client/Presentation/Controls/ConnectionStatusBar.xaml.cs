using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Edemly.Client.Presentation.Controls
{
    public partial class ConnectionStatusBar : ThemedUserControl
    {
        private ConnectionStatusVisualState _currentState = ConnectionStatusVisualState.Hidden;

        public ConnectionStatusBar()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Constructor called");
            InitializeComponent();
            Hide();
        }

        protected override void ApplyTheme()
        {
            ApplyStateVisuals();
        }

        public void ShowReconnecting()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowReconnecting called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowReconnecting (UI thread)");
                _currentState = ConnectionStatusVisualState.Reconnecting;
                StatusText.Text = DefaultLanguage.Reconnecting;
                ApplyStateVisuals();
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
                _currentState = ConnectionStatusVisualState.Disconnected;
                StatusText.Text = DefaultLanguage.ConnectionLost;
                ApplyStateVisuals();
            });
        }

        public void Hide()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Hide called");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] Hide (UI thread)");
                _currentState = ConnectionStatusVisualState.Hidden;
                StatusBorder.Visibility = Visibility.Collapsed;
                RootControl.Visibility = Visibility.Collapsed;
            });
        }

        private void ApplyStateVisuals()
        {
            if (_currentState == ConnectionStatusVisualState.Hidden)
                return;

            string backgroundResourceKey;
            string foregroundResourceKey;

            switch (_currentState)
            {
                case ConnectionStatusVisualState.Disconnected:
                    backgroundResourceKey = "ThemeDangerBrush";
                    foregroundResourceKey = "ThemeOnPrimaryTextBrush";
                    break;

                default:
                    backgroundResourceKey = "ThemeSurfaceAltBrush";
                    foregroundResourceKey = "ThemeTextPrimaryBrush";
                    break;
            }

            StatusBorder.SetResourceReference(Border.BackgroundProperty, backgroundResourceKey);
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, foregroundResourceKey);
            StatusSpinner.SetResourceReference(Shape.StrokeProperty, foregroundResourceKey);
            StatusBorder.Visibility = Visibility.Visible;
            RootControl.Visibility = Visibility.Visible;
        }

        private enum ConnectionStatusVisualState
        {
            Hidden,
            Reconnecting,
            Disconnected
        }
    }
}
