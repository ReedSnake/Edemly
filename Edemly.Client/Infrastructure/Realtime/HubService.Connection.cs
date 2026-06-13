using Microsoft.AspNetCore.SignalR.Client;
namespace Edemly.Client.Infrastructure.Realtime
{
    public partial class HubService
    {
        public async Task<bool> ConnectAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                _allowReconnect = true;
                _lastAccessToken = token;

                if (_connection != null && _connection.State == HubConnectionState.Connected)
                {
                    return true;
                }

                await DisposeExistingConnectionsAsync();

                var mainHubUrl = BuildHubUrl("main");

                _connection = HubConnectionFactory.Create(mainHubUrl, token);
                RegisterHandlers(_connection);

                if (!await TryStartWithRetriesAsync(_connection, "main"))
                {
                    throw new InvalidOperationException("Failed to start SignalR main connection");
                }

                if (!_allowReconnect || _disposed)
                {
                    await DisposeExistingConnectionsAsync();
                    OnConnectionStateChanged(false);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[HUB] Main connection state after start: {_connection?.State}");

                OnConnectionStateChanged(true);
                StartConnectionCheckTimer();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] ConnectAsync failed: {ex}");
                _allowReconnect = false;
                await DisposeExistingConnectionsAsync();
                OnConnectionStateChanged(false);
                return false;
            }
        }

        private async Task<bool> EnsureCallConnectionAsync()
        {
            try
            {
                if (_callConnection != null && _callConnection.State == HubConnectionState.Connected) return true;

                var callHubUrl = BuildHubUrl("call");
                return await StartCallConnectionAsync(callHubUrl, _lastAccessToken, "call-on-demand");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] EnsureCallConnectionAsync failed: {ex}");
                return false;
            }
        }

        private async Task<bool> StartCallConnectionAsync(string callHubUrl, string? token, string tag)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            await _callConnectionLock.WaitAsync();
            try
            {
                if (!_allowReconnect || _disposed)
                {
                    return false;
                }

                if (_callConnection != null && _callConnection.State == HubConnectionState.Connected)
                {
                    return true;
                }

                if (_callConnection != null)
                {
                    try { UnregisterCallHandlers(_callConnection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Unregister old call handlers failed: {ex}"); }
                    try { await _callConnection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose old call connection failed: {ex}"); }
                    _callConnection = null;
                }

                var conn = HubConnectionFactory.Create(callHubUrl, token);
                RegisterCallHandlers(conn);

                if (await TryStartWithRetriesAsync(conn, tag))
                {
                    if (!_allowReconnect || _disposed)
                    {
                        try { UnregisterCallHandlers(conn); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Unregister late call handlers failed: {ex}"); }
                        try { await conn.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose late call connection failed: {ex}"); }
                        return false;
                    }

                    _callConnection = conn;
                    System.Diagnostics.Debug.WriteLine($"[HUB] Call connection started via {tag}.");
                    return true;
                }

                try { UnregisterCallHandlers(conn); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Unregister failed call handlers failed: {ex}"); }
                try { await conn.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose failed call connection failed: {ex}"); }
                System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Failed to start call connection via {tag}.");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Call connection start failed via {tag}: {ex}");
                return false;
            }
            finally
            {
                _callConnectionLock.Release();
            }
        }

        private async Task DisposeExistingConnectionsAsync()
        {
            if (_connection != null)
            {
                try { UnregisterHandlers(_connection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] UnregisterHandlers failed: {ex}"); }
                try { await _connection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose old connection failed: {ex}"); }
                _connection = null;
            }

            if (_callConnection != null)
            {
                try { UnregisterCallHandlers(_callConnection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] UnregisterCallHandlers failed: {ex}"); }
                try { await _callConnection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose old call connection failed: {ex}"); }
                _callConnection = null;
            }
        }

        public async Task DisconnectAsync()
        {
            _allowReconnect = false;

            StopConnectionCheckTimer();

            if (_connection == null && _callConnection == null)
            {
                return;
            }

            try
            {
                _handlersRegisteredSet.Clear();
                _callHandlersRegisteredSet.Clear();

                if (_connection != null && (_connection.State == HubConnectionState.Connected ||
                    _connection.State == HubConnectionState.Connecting ||
                    _connection.State == HubConnectionState.Reconnecting))
                {
                    try { UnregisterHandlers(_connection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] UnregisterHandlers during disconnect failed: {ex}"); }
                    try { await _connection.StopAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Stop connection failed: {ex}"); }
                }

                if (_callConnection != null && (_callConnection.State == HubConnectionState.Connected ||
                    _callConnection.State == HubConnectionState.Connecting ||
                    _callConnection.State == HubConnectionState.Reconnecting))
                {
                    try { UnregisterCallHandlers(_callConnection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] UnregisterCallHandlers failed: {ex}"); }
                    try { await _callConnection.StopAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Stop call connection failed: {ex}"); }
                }

                if (_connection != null) { try { await _connection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose connection failed: {ex}"); } }
                if (_callConnection != null) { try { await _callConnection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose call connection failed: {ex}"); } }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] DisconnectAsync outer exception: {ex}");
            }
            finally
            {
                _connection = null;
                _callConnection = null;
                OnConnectionStateChanged(false);
            }
        }

        private void StartConnectionCheckTimer()
        {
            StopConnectionCheckTimer();

            _connectionCheckTimer = new Timer(_ =>
            {
                _ = CheckConnectionAsync();
            }, null, HubSettings.ConnectionCheckInitialDelay, HubSettings.ConnectionCheckPeriod);
        }

        private void StopConnectionCheckTimer()
        {
            if (_connectionCheckTimer != null)
            {
                _connectionCheckTimer.Dispose();
                _connectionCheckTimer = null;
            }
        }

        private Task CheckConnectionAsync()
        {
            if (_connection == null || _disposed)
                return Task.CompletedTask;

            var currentState = _connection.State;

            if (currentState != HubConnectionState.Connected && !_isReconnecting)
            {
                OnConnectionStateChanged(false);
            }

            return Task.CompletedTask;
        }

        private async Task<bool> TryStartWithRetriesAsync(HubConnection? conn, string tag)
        {
            if (conn == null) return false;
            const int maxAttempts = 2;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt}...");
                    using var cts = new CancellationTokenSource(HubSettings.StartConnectionTimeout);
                    await conn.StartAsync(cts.Token);
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Started. State={conn.State}");
                    return true;
                }
                catch (TaskCanceledException tce)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt} canceled/timeout: {tce.Message}");
                    if (attempt < maxAttempts) await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt} failed: {ex.Message}");
                    if (attempt < maxAttempts) await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                }
            }
            return false;
        }

        private async Task OnConnectionClosedInternalAsync(HubConnection conn, Exception? error)
        {
            if (!ReferenceEquals(conn, _connection))
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Non-main connection closed (ignored for global state)");
                return;
            }

            if (!_allowReconnect)
            {
                _isReconnecting = false;
                OnConnectionStateChanged(false);
                return;
            }

            _isReconnecting = false;
            OnConnectionStateChanged(false);
        }

        private Task OnReconnectingInternal(HubConnection conn, Exception? error)
        {
            if (!ReferenceEquals(conn, _connection))
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Non-main connection entering reconnecting (ignored)");
                return Task.CompletedTask;
            }

            if (!_allowReconnect)
            {
                return Task.CompletedTask;
            }

            _isReconnecting = true;
            OnConnectionStateChanged(false);
            return Task.CompletedTask;
        }

        private Task OnReconnectedInternal(HubConnection conn, string? connectionId)
        {
            if (!ReferenceEquals(conn, _connection))
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Non-main connection reconnected (ignored)");
                return Task.CompletedTask;
            }

            if (!_allowReconnect)
            {
                _ = DisconnectAsync();
                return Task.CompletedTask;
            }

            _isReconnecting = false;
            OnConnectionStateChanged(true);
            return Task.CompletedTask;
        }

        private async Task OnConnectionClosedAsync(Exception? error)
        {
            if (!_allowReconnect)
            {
                _isReconnecting = false;
                OnConnectionStateChanged(false);
                return;
            }

            _isReconnecting = false;
            OnConnectionStateChanged(false);
        }
    }
}
