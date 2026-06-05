using Edemly.Client.Infrastructure.Realtime;
using System.Net.Http;
namespace Edemly.Client.Application.Services
{
    public class ServerHealthService : IDisposable
    {
        private readonly string _serverUrl;
        private readonly HttpClient _httpClient;
        private Timer? _healthCheckTimer;
        private bool _disposed;
        private bool _isChecking;
        private bool _lastKnownState;

        public event Action<bool>? ServerAvailabilityChanged;

        public bool IsServerAvailable { get; private set; }

        public ServerHealthService(string serverUrl)
        {
            _serverUrl = serverUrl;
            _httpClient = new HttpClient
            {
                Timeout = HubSettings.ConnectionCheckInitialDelay
            };
            _lastKnownState = false;
            IsServerAvailable = false;
        }

        public void StartHealthCheck()
        {
            if (_healthCheckTimer != null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[SERVER HEALTH] Starting health check...");

            _ = CheckServerHealthAsync();

            _healthCheckTimer = new Timer(async _ =>
            {
                await CheckServerHealthAsync();
            }, null, HubSettings.ConnectionCheckInitialDelay, HubSettings.ConnectionCheckInitialDelay);
        }

        public void StopHealthCheck()
        {
            if (_healthCheckTimer != null)
            {
                _healthCheckTimer.Dispose();
                _healthCheckTimer = null;
                System.Diagnostics.Debug.WriteLine("[SERVER HEALTH] Health check stopped");
            }
        }

        public async Task<bool> CheckServerHealthAsync()
        {
            if (_isChecking || _disposed)
            {
                return IsServerAvailable;
            }

            _isChecking = true;

            try
            {
                var response = await _httpClient.GetAsync(_serverUrl);

                bool isAvailable = response.IsSuccessStatusCode;

                if (isAvailable != _lastKnownState)
                {
                    _lastKnownState = isAvailable;
                    IsServerAvailable = isAvailable;

                    System.Diagnostics.Debug.WriteLine($"[SERVER HEALTH] Server state changed: {(isAvailable ? "AVAILABLE ?" : "UNAVAILABLE ?")}");

                    OnServerAvailabilityChanged(isAvailable);
                }

                return isAvailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SERVER HEALTH] Health check failed: {ex.Message}");

                if (_lastKnownState != false)
                {
                    _lastKnownState = false;
                    IsServerAvailable = false;
                    System.Diagnostics.Debug.WriteLine("[SERVER HEALTH] Server state changed: UNAVAILABLE ?");
                    OnServerAvailabilityChanged(false);
                }

                return false;
            }
            finally
            {
                _isChecking = false;
            }
        }

        private void OnServerAvailabilityChanged(bool isAvailable)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ServerAvailabilityChanged?.Invoke(isAvailable);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopHealthCheck();
            _httpClient?.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}