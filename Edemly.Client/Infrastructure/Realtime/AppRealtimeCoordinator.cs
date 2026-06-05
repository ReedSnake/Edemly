using Edemly.Client.Presentation.Controls;
using Edemly.Contracts.Realtime;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Infrastructure.Realtime
{
    public sealed class AppRealtimeCoordinator
    {
        private readonly Func<IHubService> _hubServiceProvider;
        private readonly Func<HubService?> _concreteHubProvider;
        private readonly Func<ConnectionStatusBar?> _statusBarProvider;
        private readonly Func<string?> _authTokenProvider;
        private readonly Action<IncomingCallEventDto> _incomingCallHandler;

        public AppRealtimeCoordinator(
            Func<IHubService> hubServiceProvider,
            Func<HubService?> concreteHubProvider,
            Func<ConnectionStatusBar?> statusBarProvider,
            Func<string?> authTokenProvider,
            Action<IncomingCallEventDto> incomingCallHandler)
        {
            _hubServiceProvider = hubServiceProvider ?? throw new ArgumentNullException(nameof(hubServiceProvider));
            _concreteHubProvider = concreteHubProvider ?? throw new ArgumentNullException(nameof(concreteHubProvider));
            _statusBarProvider = statusBarProvider ?? throw new ArgumentNullException(nameof(statusBarProvider));
            _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
            _incomingCallHandler = incomingCallHandler ?? throw new ArgumentNullException(nameof(incomingCallHandler));
        }

        public void SubscribeHubEvents()
        {
            try
            {
                var concreteHub = _concreteHubProvider();
                if (concreteHub != null)
                {
                    concreteHub.IncomingCallReceived -= OnIncomingCallReceived;
                    concreteHub.IncomingCallReceived += OnIncomingCallReceived;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP REALTIME] Failed to subscribe incoming call handler: {ex}");
            }

            try
            {
                var hubService = _hubServiceProvider();
                hubService.ConnectionStateChanged -= OnConnectionStateChanged;
                hubService.ConnectionStateChanged += OnConnectionStateChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP REALTIME] Failed to subscribe connection state: {ex}");
            }

            RefreshConnectionState();
        }

        public void UnsubscribeHubEvents()
        {
            try
            {
                var hubService = _hubServiceProvider();
                hubService.ConnectionStateChanged -= OnConnectionStateChanged;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP REALTIME] Failed to unsubscribe connection state: {ex}");
            }

            try
            {
                var concreteHub = _concreteHubProvider();
                if (concreteHub != null)
                {
                    concreteHub.IncomingCallReceived -= OnIncomingCallReceived;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP REALTIME] Failed to unsubscribe incoming call handler: {ex}");
            }
        }

        public void RefreshConnectionState()
        {
            try
            {
                var hubService = _hubServiceProvider();
                OnConnectionStateChanged(hubService.IsConnected);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP REALTIME] RefreshConnectionState failed: {ex}");
            }
        }

        private void OnIncomingCallReceived(IncomingCallEventDto data)
        {
            _incomingCallHandler(data);
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            var application = System.Windows.Application.Current;
            if (application == null)
            {
                return;
            }

            void UpdateStatusBar()
            {
                var statusBar = _statusBarProvider();
                if (statusBar == null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(_authTokenProvider()))
                {
                    statusBar.Hide();
                    return;
                }

                if (isConnected)
                {
                    statusBar.ShowConnected();
                    return;
                }

                bool isReconnecting = false;
                try
                {
                    isReconnecting = _concreteHubProvider()?.IsReconnecting ?? false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APP REALTIME] Failed to read reconnecting state: {ex}");
                }

                if (isReconnecting)
                {
                    statusBar.ShowReconnecting();
                }
                else
                {
                    statusBar.Hide();
                }
            }

            if (application.Dispatcher.CheckAccess())
            {
                UpdateStatusBar();
                return;
            }

            application.Dispatcher.Invoke(UpdateStatusBar);
        }
    }
}
