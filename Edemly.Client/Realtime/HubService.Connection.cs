using Microsoft.AspNetCore.SignalR.Client;

namespace Edemly.Client.Realtime
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

                if (_connection != null)
                {
                    // Unregister handlers from the old connection before disposing
                    try { UnregisterHandlers(_connection); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] UnregisterHandlers failed: {ex}"); }
                    try { await _connection.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose old connection failed: {ex}"); }
                    _connection = null;
                }

                var mainHubUrl = BuildHubUrl("main");
                var callHubUrl = BuildHubUrl("call");

                _connection = HubConnectionFactory.Create(mainHubUrl, token);

                // Also create a separate connection to the call hub endpoint (/call)
                try
                {
                    _callConnection = HubConnectionFactory.Create(callHubUrl, token);
                }
                catch (Exception ex)
                {
                    _callConnection = null;
                    System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Failed to build call connection: {ex}");
                }

                // Register handlers for the freshly created main connection
                RegisterHandlers(_connection);

                // Register call-related handlers on call connection if created
                if (_callConnection != null)
                {
                    RegisterCallHandlers(_callConnection);
                }
                else
                {
                    // No dedicated call connection available; register call handlers on the main connection
                    try
                    {
                        RegisterCallHandlers(_connection);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Failed to register call handlers on main connection: {ex}");
                    }
                }

                // Try WebSockets-first (skip negotiation) with retries; fall back to default negotiated connection if fails
                bool started = false;

                // Build a WebSockets-first candidate
                try
                {
                    var wsCandidate = HubConnectionFactory.Create(mainHubUrl, token, webSocketsOnly: true);

                    System.Diagnostics.Debug.WriteLine("[HUB] Trying WebSockets-first connection (skip negotiation)...");
                    started = await TryStartWithRetriesAsync(wsCandidate, "ws-first");
                    if (started)
                    {
                        try { if (_connection != null) { UnregisterHandlers(_connection); await _connection.DisposeAsync(); } } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to dispose previous connection: {ex}"); }
                        _connection = wsCandidate;
                        System.Diagnostics.Debug.WriteLine("[HUB] Using WebSockets-first connection.");
                        // Ensure handlers are attached to the ws-first connection
                        RegisterHandlers(_connection);
                        // If there is no dedicated call connection, ensure call handlers are attached to the main connection
                        try
                        {
                            if (_callConnection == null)
                            {
                                RegisterCallHandlers(_connection);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Failed to register call handlers on ws-first main connection: {ex}");
                        }
                    }
                    else
                    {
                        try { await wsCandidate.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose wsCandidate failed: {ex}"); }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][WARN] WebSockets-first build/start failed: {ex}");
                }

                if (!started)
                {
                    System.Diagnostics.Debug.WriteLine("[HUB] Falling back to default negotiated connection...");
                    started = await TryStartWithRetriesAsync(_connection, "negotiated");
                    if (!started)
                    {
                        throw new InvalidOperationException("Failed to start SignalR connection (both ws-first and negotiated attempts failed)");
                    }
                }

                if (_callConnection != null)
                {
                    var wsCallCandidate = HubConnectionFactory.Create(callHubUrl, token, webSocketsOnly: true);

                    bool callStarted = false;
                    try
                    {
                        callStarted = await TryStartWithRetriesAsync(wsCallCandidate, "call-ws-first");
                        if (callStarted)
                        {
                            try { if (_callConnection != null) { UnregisterCallHandlers(_callConnection); await _callConnection.DisposeAsync(); } } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to dispose previous call connection: {ex}"); }
                            _callConnection = wsCallCandidate;
                            // attach call handlers to the ws-first call connection
                            RegisterCallHandlers(_callConnection);
                        }
                        else
                        {
                            try { await wsCallCandidate.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose wsCallCandidate failed: {ex}"); }
                            // try original negotiated _callConnection
                            callStarted = await TryStartWithRetriesAsync(_callConnection, "call-negotiated");
                            if (!callStarted)
                            {
                                System.Diagnostics.Debug.WriteLine("[HUB][WARN] Failed to start call connection (both ws-first and negotiated)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB][WARN] Call connection attempts failed: {ex}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[HUB][WARN] Call connection was not created (null).");
                }

                System.Diagnostics.Debug.WriteLine($"[HUB] Main connection state after start: {_connection?.State}; Call connection state: {_callConnection?.State}");

                OnConnectionStateChanged(true);
                StartConnectionCheckTimer();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] ConnectAsync failed: {ex}");
                OnConnectionStateChanged(false);
                return false;
            }
        }

        // Ensure a call-specific connection exists and is started. Returns true if ready.
        private async Task<bool> EnsureCallConnectionAsync()
        {
            try
            {
                if (_callConnection != null && _callConnection.State == HubConnectionState.Connected) return true;

                var callHubUrl = BuildHubUrl("call");

                var conn = HubConnectionFactory.Create(callHubUrl, _lastAccessToken);

                RegisterCallHandlers(conn);

                var started = await TryStartWithRetriesAsync(conn, "call-temp");
                if (started)
                {
                    try { if (_callConnection != null) { UnregisterCallHandlers(_callConnection); await _callConnection.DisposeAsync(); } } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to dispose previous call connection: {ex}"); }
                    _callConnection = conn;
                    System.Diagnostics.Debug.WriteLine("[HUB] Call connection created and started on demand.");
                    return true;
                }

                try { await conn.DisposeAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose conn failed: {ex}"); }
                System.Diagnostics.Debug.WriteLine("[HUB][WARN] Failed to start on-demand call connection.");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] EnsureCallConnectionAsync failed: {ex}");
                return false;
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
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt}...");
                    var cts = new CancellationTokenSource(HubSettings.StartConnectionTimeout);
                    await conn.StartAsync(cts.Token);
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Started. State={conn.State}");
                    return true;
                }
                catch (TaskCanceledException tce)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt} canceled/timeout: {tce.Message}");
                    if (attempt < maxAttempts) await Task.Delay(TimeSpan.FromSeconds(1 * attempt));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt} failed: {ex.Message}");
                    if (attempt < maxAttempts) await Task.Delay(TimeSpan.FromSeconds(1 * attempt));
                }
            }
            return false;
        }

        private async Task OnConnectionClosedInternalAsync(HubConnection conn, Exception? error)
        {
            // Only consider main connection for global reconnecting state
            if (!ReferenceEquals(conn, _connection))
            {
                // For non-main connections, just log and ignore
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
            // Only consider main connection for global reconnecting state
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
            // Only consider main connection for global reconnecting state
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
