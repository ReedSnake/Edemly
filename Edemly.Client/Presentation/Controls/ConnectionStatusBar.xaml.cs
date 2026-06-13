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
        private bool _updateDismissed;
        private string _updateVersion = string.Empty;

        public event EventHandler? UpdateNowRequested;
        public event EventHandler? UpdateDismissed;

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
                if (IsUpdateState())
                {
                    return;
                }

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
                if (IsUpdateState())
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowConnected (UI thread, hiding status bar)");
                Hide();
            });
        }

        public void ShowDisconnected()
        {
            System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowDisconnected called");
            Dispatcher.Invoke(() =>
            {
                if (IsUpdateState())
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[CONNECTION_STATUS_BAR] ShowDisconnected (UI thread)");
                _currentState = ConnectionStatusVisualState.Disconnected;
                StatusText.Text = DefaultLanguage.ConnectionLost;
                ApplyStateVisuals();
            });
        }

        public void HideConnectionStatus()
        {
            Dispatcher.Invoke(() =>
            {
                if (IsUpdateState())
                {
                    return;
                }

                Hide();
            });
        }

        public void ShowUpdateAvailable(string version, bool mandatory)
        {
            Dispatcher.Invoke(() =>
            {
                if (_updateDismissed && !mandatory)
                {
                    return;
                }

                _updateVersion = version;
                _currentState = mandatory
                    ? ConnectionStatusVisualState.UpdateMandatory
                    : ConnectionStatusVisualState.UpdateAvailable;
                ApplyUpdateText();
                ApplyStateVisuals();
            });
        }

        public void ShowUpdateInstalling(string version, int? progress = null, bool mandatory = false)
        {
            Dispatcher.Invoke(() =>
            {
                _updateVersion = version;
                _currentState = ConnectionStatusVisualState.UpdateInstalling;
                StatusText.Text = progress.HasValue
                    ? string.Format(DefaultLanguage.UpdateDownloadingProgress, version, progress.Value)
                    : string.Format(DefaultLanguage.UpdateInstalling, version);
                ApplyStateVisuals();
            });
        }

        public void ShowUpdateFailed(string message, bool mandatory)
        {
            Dispatcher.Invoke(() =>
            {
                _currentState = mandatory
                    ? ConnectionStatusVisualState.UpdateMandatory
                    : ConnectionStatusVisualState.UpdateAvailable;
                StatusText.Text = string.Format(DefaultLanguage.UpdateFailed, message);
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
            bool spinnerVisible = true;
            bool updateActionsVisible = false;
            bool dismissVisible = false;
            bool updateButtonsEnabled = true;

            switch (_currentState)
            {
                case ConnectionStatusVisualState.Disconnected:
                    backgroundResourceKey = "ThemeDangerBrush";
                    foregroundResourceKey = "ThemeOnPrimaryTextBrush";
                    break;

                case ConnectionStatusVisualState.UpdateAvailable:
                    backgroundResourceKey = "ThemeInfoBrush";
                    foregroundResourceKey = "ThemeOnPrimaryTextBrush";
                    spinnerVisible = false;
                    updateActionsVisible = true;
                    dismissVisible = true;
                    break;

                case ConnectionStatusVisualState.UpdateMandatory:
                    backgroundResourceKey = "ThemeWarningBrush";
                    foregroundResourceKey = "ThemeOnPrimaryTextBrush";
                    spinnerVisible = false;
                    updateActionsVisible = true;
                    dismissVisible = false;
                    RemindLaterButton.Visibility = Visibility.Collapsed;
                    break;

                case ConnectionStatusVisualState.UpdateInstalling:
                    backgroundResourceKey = "ThemeInfoBrush";
                    foregroundResourceKey = "ThemeOnPrimaryTextBrush";
                    updateButtonsEnabled = false;
                    break;

                default:
                    backgroundResourceKey = "ThemeSurfaceAltBrush";
                    foregroundResourceKey = "ThemeTextPrimaryBrush";
                    break;
            }

            StatusBorder.SetResourceReference(Border.BackgroundProperty, backgroundResourceKey);
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, foregroundResourceKey);
            StatusSpinner.SetResourceReference(Shape.StrokeProperty, foregroundResourceKey);
            StatusSpinnerHost.Visibility = spinnerVisible ? Visibility.Visible : Visibility.Collapsed;
            UpdateActionsPanel.Visibility = updateActionsVisible ? Visibility.Visible : Visibility.Collapsed;
            DismissButton.Visibility = dismissVisible ? Visibility.Visible : Visibility.Collapsed;
            UpdateNowButton.IsEnabled = updateButtonsEnabled;
            RemindLaterButton.IsEnabled = updateButtonsEnabled;
            if (_currentState != ConnectionStatusVisualState.UpdateMandatory)
            {
                RemindLaterButton.Visibility = Visibility.Visible;
            }

            StatusBorder.Visibility = Visibility.Visible;
            RootControl.Visibility = Visibility.Visible;
        }

        private void ApplyUpdateText()
        {
            StatusText.Text = _currentState == ConnectionStatusVisualState.UpdateMandatory
                ? string.Format(DefaultLanguage.UpdateMandatoryAvailable, _updateVersion)
                : string.Format(DefaultLanguage.UpdateAvailable, _updateVersion);
        }

        private bool IsUpdateState()
        {
            return _currentState == ConnectionStatusVisualState.UpdateAvailable ||
                   _currentState == ConnectionStatusVisualState.UpdateMandatory ||
                   _currentState == ConnectionStatusVisualState.UpdateInstalling;
        }

        private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateNowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
        {
            DismissUpdate();
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            DismissUpdate();
        }

        private void DismissUpdate()
        {
            if (_currentState == ConnectionStatusVisualState.UpdateMandatory ||
                _currentState == ConnectionStatusVisualState.UpdateInstalling)
            {
                return;
            }

            _updateDismissed = true;
            UpdateDismissed?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        private enum ConnectionStatusVisualState
        {
            Hidden,
            Reconnecting,
            Disconnected,
            UpdateAvailable,
            UpdateMandatory,
            UpdateInstalling
        }
    }
}
