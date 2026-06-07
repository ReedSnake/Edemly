using Edemly.Client.Api;
using System.Diagnostics;
using System.Windows;

namespace Edemly.Client.Application.Calls
{
    public sealed class CallWindowCoordinator
    {
        private readonly Func<IHubService> _hubServiceProvider;
        private readonly Func<IApiClients> _apiClientProvider;
        private readonly Func<string?> _authTokenProvider;
        private readonly Func<Window?> _mainWindowProvider;

        public CallWindowCoordinator(
            Func<IHubService> hubServiceProvider,
            Func<IApiClients> apiClientProvider,
            Func<string?> authTokenProvider,
            Func<Window?> mainWindowProvider)
        {
            _hubServiceProvider = hubServiceProvider ?? throw new ArgumentNullException(nameof(hubServiceProvider));
            _apiClientProvider = apiClientProvider ?? throw new ArgumentNullException(nameof(apiClientProvider));
            _authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
            _mainWindowProvider = mainWindowProvider ?? throw new ArgumentNullException(nameof(mainWindowProvider));
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

                var calls = await _apiClientProvider().Calls.GetActiveCallsAsync();
                if (calls == null || calls.Count == 0)
                {
                    return;
                }

                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
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
                            Debug.WriteLine($"[CALL COORDINATOR] Activate existing CallWindow failed: {ex}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALL COORDINATOR] EnsureHubConnectedAndRestoreCallsAsync failed: {ex}");
            }
        }

        public void HandleIncomingCall(IncomingCallEventDto data)
        {
            try
            {
                Debug.WriteLine(
                    $"[CALL COORDINATOR] Incoming call. callId={data?.CallId} callUid={data?.CallUid} metadata={data?.Metadata}");

                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
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

            var callWindow = new CallWindow
            {
                Owner = _mainWindowProvider()
            };

            callWindow.RegisterHubHandlers();
            return callWindow;
        }
    }
}
