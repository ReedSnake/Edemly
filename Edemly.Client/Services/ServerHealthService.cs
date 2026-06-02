using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace uchat.Services
{
    /// <summary>
    /// Сервіс для перевірки доступності сервера
    /// </summary>
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
                Timeout = TimeSpan.FromSeconds(3)
            };
            _lastKnownState = false;
            IsServerAvailable = false;
        }

        /// <summary>
        /// Запускає періодичну перевірку доступності сервера
        /// </summary>
        public void StartHealthCheck()
        {
            if (_healthCheckTimer != null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[SERVER HEALTH] Starting health check...");

            // Перша перевірка відразу
            _ = CheckServerHealthAsync();

            // Потім перевіряємо кожні 3 секунди
            _healthCheckTimer = new Timer(async _ =>
            {
                await CheckServerHealthAsync();
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Зупиняє перевірку доступності сервера
        /// </summary>
        public void StopHealthCheck()
        {
            if (_healthCheckTimer != null)
            {
                _healthCheckTimer.Dispose();
                _healthCheckTimer = null;
                System.Diagnostics.Debug.WriteLine("[SERVER HEALTH] Health check stopped");
            }
        }

        /// <summary>
        /// Перевіряє доступність сервера один раз
        /// </summary>
        public async Task<bool> CheckServerHealthAsync()
        {
            if (_isChecking || _disposed)
            {
                return IsServerAvailable;
            }

            _isChecking = true;

            try
            {
                // Пробуємо зробити запит до кореневого URL сервера
                var response = await _httpClient.GetAsync(_serverUrl);
                
                bool isAvailable = response.IsSuccessStatusCode;
                
                // Оновлюємо стан якщо він змінився
                if (isAvailable != _lastKnownState)
                {
                    _lastKnownState = isAvailable;
                    IsServerAvailable = isAvailable;
                    
                    System.Diagnostics.Debug.WriteLine($"[SERVER HEALTH] Server state changed: {(isAvailable ? "AVAILABLE ?" : "UNAVAILABLE ?")}");
                    
                    // Викликаємо подію зміни стану
                    OnServerAvailabilityChanged(isAvailable);
                }

                return isAvailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SERVER HEALTH] Health check failed: {ex.Message}");
                
                // Сервер недоступний
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
