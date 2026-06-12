using Edemly.Client.Infrastructure.Realtime;
using Edemly.Client.Presentation.Windows.Calls;
using Edemly.Contracts.Realtime;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Application.Calls
{
    public sealed class CallWindowCoordinator
    {
        private readonly Func<IHubService> _hubServiceProvider;
        private readonly Func<string?> _authTokenProvider;
        private readonly Func<Window?> _mainWindowProvider;
        private readonly CallSessionController _callSessionController;

        public CallWindowCoordinator(
            Func<IHubService> hubServiceProvider,
            Func<string?> authTokenProvider,
            Func<Window?> mainWindowProvider,
            CallSessionController callSessionController)
        {
            _hubServiceProvider = hubServiceProvider ?? throw new ArgumentNullException(nameof(hubServiceProvider));
            _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
            _mainWindowProvider = mainWindowProvider ?? throw new ArgumentNullException(nameof(mainWindowProvider));
            _callSessionController = callSessionController ?? throw new ArgumentNullException(nameof(callSessionController));
        }

        public async Task EnsureHubConnectedAndRestoreCallsAsync()
        {
            try
            {
                var hubService = _hubServiceProvider();
                var authToken = _authTokenProvider();

                if (!hubService.IsConnected && !string.IsNullOrWhiteSpace(authToken))
                {
                    try
                    {
                        await hubService.ConnectAsync(authToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALL COORDINATOR] Failed to connect hub: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALL COORDINATOR] EnsureHubConnectedAsync failed: {ex}");
            }
        }

        public void HandleIncomingCall(IncomingCallEventDto data)
        {
            try
            {
                if (data == null)
                {
                    Debug.WriteLine("[CALL COORDINATOR] Incoming call data is null");
                    return;
                }

                Debug.WriteLine(
                    $"[CALL COORDINATOR] Incoming call. callId={data.CallId} callUid={data.CallUid} metadata={data.Metadata}");

                if (_callSessionController.ShouldIgnoreIncoming(data))
                {
                    Debug.WriteLine("[CALL COORDINATOR] Incoming call ignored before opening call window");
                    return;
                }

                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_callSessionController.ShouldIgnoreIncoming(data))
                        {
                            Debug.WriteLine("[CALL COORDINATOR] Incoming call ignored on UI dispatcher");
                            return;
                        }

                        var callWindow = GetOrCreateCallWindow();
                        if (!callWindow.IsVisible)
                        {
                            callWindow.Show();
                        }
                        else
                        {
                            try
                            {
                                callWindow.Activate();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[CALL COORDINATOR] Activate CallWindow failed: {ex}");
                            }
                        }

                        callWindow.HandleIncomingCall(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALL COORDINATOR] Incoming call UI error: {ex}");
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALL COORDINATOR] HandleIncomingCall failed: {ex}");
            }
        }

        private CallWindow GetOrCreateCallWindow()
        {
            var existing = System.Windows.Application.Current?.Windows.OfType<CallWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.RegisterHubHandlers();
                if (existing.Owner == null)
                {
                    existing.Owner = _mainWindowProvider();
                }

                return existing;
            }

            var callWindow = new CallWindow(_callSessionController)
            {
                Owner = _mainWindowProvider()
            };

            callWindow.RegisterHubHandlers();
            return callWindow;
        }
    }
}
