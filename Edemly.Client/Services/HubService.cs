using CommunityToolkit.WinUI.Notifications;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using uchat.DTOs;
using uchat.Pages;
using uchat.Services;

namespace uchat.Services
{
    public class HubService : IHubService
    {
        private HubConnection? _connection;
        private HubConnection? _callConnection;
        private readonly string _hubUrl;
        private bool _disposed;
        private Timer? _connectionCheckTimer;
        private bool _lastConnectionState = false;
        private bool _isReconnecting;

        private bool _allowReconnect = true;
        // Track per-connection registration to ensure handlers are attached to each HubConnection instance
        private readonly System.Collections.Generic.HashSet<HubConnection> _handlersRegisteredSet = new System.Collections.Generic.HashSet<HubConnection>();
        private readonly System.Collections.Generic.HashSet<HubConnection> _callHandlersRegisteredSet = new System.Collections.Generic.HashSet<HubConnection>();
        private readonly object _stateLock = new object();
        private string? _lastAccessToken;

        // Message events
        public event Action<MessageDto>? MessageReceived;
        public event Action<MessageDto>? MessageUpdated;
        public event Action<int, int>? MessageDeleted;
        public event Action<bool>? ConnectionStateChanged;
        public event Action<int>? GroupCreated;
        public event Action<int, string?, string?, string?>? GroupUpdated; // chatId, name, description, iconUrl
        public event Action<int, bool, DateTime?>? UserStatusChanged;
        public event Action<int, string>? ProfileUpdated; // ДОДАНО

        // Call events
        public event Action<IncomingCallData>? IncomingCallReceived;
        public event Action<int, int>? CallAcceptedReceived; // callId, userId
        public event Action<int, int, string?>? CallRejectedReceived; // callId, userId, reason
        public event Action<int, int>? CallEndedReceived; // callId, userId
        public event Action<SignalData>? OfferReceived;
        public event Action<SignalData>? AnswerReceived;
        public event Action<SignalIce>? IceCandidateReceived;

        // New: calling indicator for initiator
        public event Action<int, string?>? CallingReceived; // callId, callUid

        // Audio streaming events - now include sequenceId and timestamp (ms since epoch)
        public event Action<int, byte[], int, long, long>? AudioChunkReceived; // fromUserId, chunk, callId, sequenceId, timestampMs

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        // New: expose whether call-specific connection is connected (useful for diagnostics)
        public bool IsCallConnected => _callConnection?.State == HubConnectionState.Connected;

        // New: expose whether client is currently in reconnecting state
        public bool IsReconnecting => _isReconnecting;

        // Internal lifecycle handlers that are aware of which connection raised the event
        private async Task OnConnectionClosedInternal(HubConnection conn, Exception? error)
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

        public HubService(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            var baseUrl = serverUrl.TrimEnd('/');
            _hubUrl = $"{baseUrl}/main";
        }

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

                // Append tenant query parameter if configured so server can resolve tenant for SignalR
                var hubUrlWithTenant = _hubUrl;
                try
                {
                    var cfg = ConfigService.Instance;
                    if (cfg != null && cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company))
                    {
                        var tenant = Uri.EscapeDataString(cfg.Company.Trim());
                        hubUrlWithTenant = _hubUrl + "?tenant=" + tenant;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to read config for tenant: {ex}"); }

                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrlWithTenant, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                // Also create a separate connection to the call hub endpoint (/call)
                try
                {
                    var callHubUrl = hubUrlWithTenant.Replace("/main", "/call");
                    _callConnection = new HubConnectionBuilder()
                        .WithUrl(callHubUrl, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                        })
                        .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                        .Build();
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
                    var wsCandidate = new HubConnectionBuilder()
                        .WithUrl(hubUrlWithTenant, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                            try { options.SkipNegotiation = true; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to set SkipNegotiation: {ex}"); }
                            try
                            {
                                options.WebSocketConfiguration = ws => ws.KeepAliveInterval = TimeSpan.FromSeconds(20);
                                options.HttpMessageHandlerFactory = _ => new System.Net.Http.HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to configure WebSocket options: {ex}"); }
                        })
                        .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                        .Build();

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
                    // try WebSockets-first for call connection as well
                    var callHubUrl = hubUrlWithTenant.Replace("/main", "/call");
                    var wsCallCandidate = new HubConnectionBuilder()
                        .WithUrl(callHubUrl, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                            try { options.SkipNegotiation = true; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to set SkipNegotiation: {ex}"); }
                            try
                            {
                                options.WebSocketConfiguration = ws => ws.KeepAliveInterval = TimeSpan.FromSeconds(20);
                                options.HttpMessageHandlerFactory = _ => new System.Net.Http.HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to configure WebSocket options: {ex}"); }
                        })
                        .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                        .Build();

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

                // Build hub URL with tenant if configured
                var hubUrlWithTenant = _hubUrl;
                try
                {
                    var cfg = ConfigService.Instance;
                    if (cfg != null && cfg.IsInstalled && !string.IsNullOrWhiteSpace(cfg.Company))
                    {
                        var tenant = Uri.EscapeDataString(cfg.Company.Trim());
                        hubUrlWithTenant = _hubUrl + "?tenant=" + tenant;
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to resolve tenant for call connection: {ex.Message}"); }

                var callHubUrl = hubUrlWithTenant.Replace("/main", "/call");

                var builder = new HubConnectionBuilder()
                    .WithUrl(callHubUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_lastAccessToken);
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) });

                var conn = builder.Build();

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
                CheckConnectionAsync();
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
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

        public async Task<bool> SendMessageAsync(MessageCreateDto message)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[HUB] SendMessageAsync called while not connected");
                return false;
            }

            try
            {
                // log payload for debugging
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(message);
                    System.Diagnostics.Debug.WriteLine($"[HUB] Invoking SendMessage. ConnectionState={_connection?.State}; Payload={json}");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to serialize message for debug output: {ex}"); }

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _connection!.InvokeAsync("SendMessage", message, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                // Detailed logging to help diagnose why messages aren't delivered
                try
                {
                    var state = _connection?.State.ToString() ?? "<null>";
                    System.Diagnostics.Debug.WriteLine($"[HUB][ERROR] SendMessageAsync failed. ConnectionState={state}. Exception={ex}");
                }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to log SendMessageAsync error: {logEx}"); }

                ShowError("Помилка надсилання повідомлення", ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateMessageAsync(MessageUpdateDto message)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await _connection!.InvokeAsync("UpdateMessage", message, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Помилка оновлення повідомлення", ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(int messageId, int chatId)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await _connection!.InvokeAsync("DeleteMessage", messageId, chatId, cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Помилка видалення повідомлення", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Повідомити сервер про оновлення профілю
        /// </summary>
        public async Task<bool> NotifyProfileUpdateAsync(int userId, string newPfpUrl)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                await _connection!.InvokeAsync("NotifyProfileUpdated", userId, newPfpUrl);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify profile update: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Повідомити сервер про оновлення групи
        /// </summary>
        public async Task<bool> NotifyGroupUpdateAsync(int chatId, string? name, string? description, string? iconUrl)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                await _connection!.InvokeAsync("NotifyGroupUpdated", chatId, name, description, iconUrl);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify group update: {ex.Message}");
                return false;
            }
        }

        private void RegisterHandlers(HubConnection? conn)
        {
            if (conn == null) return;

            lock (_stateLock)
            {
                if (_handlersRegisteredSet.Contains(conn)) return;
                _handlersRegisteredSet.Add(conn);
            }

            conn.On<MessageDto>("ReceiveMessage", (message) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageReceived?.Invoke(message);

                    try
                    {
                        // Do not show toast if message is from current user
                        var isFromMe = App.CurrentUserId.HasValue && App.CurrentUserId.Value == message.SenderId;

                        // Do not show toast if user currently views this chat
                        var currentChat = MyInfo.currentChatIdNotification;

                        if (!isFromMe && message.ChatId != currentChat)
                        {
                            ShowToast(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Error in ReceiveMessage handler: {ex.Message}");
                    }
                });
            });


            conn.On<MessageDto>("ReceiveMessageUpdate", (message) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    {
                        MessageUpdated?.Invoke(message);
                    }
                });
            });

            //reminding notif confirmation

            conn.On<int>("SendNotifyReminder", async reminderId =>
            {
                try
                {
                    if (reminderId != 0)
                    {
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await ShowReminderToast(reminderId);
                        });

                        await conn.InvokeAsync("ConfirmRemindingReceived", reminderId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to handle reminder notification: {ex}");
                }
            });

            // ReceiveMessageDelete can be invoked with two integers (messageId, chatId).
            // Register a typed handler to avoid JSON parsing/casing issues.
            conn.On<int, int>("ReceiveMessageDelete", (messageId, chatId) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageDeleted?.Invoke(messageId, chatId);
                });
            });

            conn.On<object>("GroupCreated", (data) =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var groupData = System.Text.Json.JsonSerializer.Deserialize<GroupChatCreatedDto>(json);

                if (groupData != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GroupCreated?.Invoke(groupData.ChatId);
                    });
                }
            });

            // Обробник оновлення групи
            conn.On<object>("GroupUpdated", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    System.Diagnostics.Debug.WriteLine($"[HUB RAW] GroupUpdated payload: {json}");

                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    int chatId = 0;
                    string? name = null;
                    string? description = null;
                    string? iconUrl = null;

                    if (root.TryGetProperty("chatId", out var e1) && e1.ValueKind == System.Text.Json.JsonValueKind.Number)
                        chatId = e1.GetInt32();
                    else if (root.TryGetProperty("ChatId", out var e12) && e12.ValueKind == System.Text.Json.JsonValueKind.Number)
                        chatId = e12.GetInt32();

                    if (root.TryGetProperty("name", out var e2) && e2.ValueKind == System.Text.Json.JsonValueKind.String)
                        name = e2.GetString();
                    else if (root.TryGetProperty("Name", out var e22) && e22.ValueKind == System.Text.Json.JsonValueKind.String)
                        name = e22.GetString();

                    if (root.TryGetProperty("description", out var e3) && e3.ValueKind == System.Text.Json.JsonValueKind.String)
                        description = e3.GetString();
                    else if (root.TryGetProperty("Description", out var e32) && e32.ValueKind == System.Text.Json.JsonValueKind.String)
                        description = e32.GetString();

                    if (root.TryGetProperty("iconUrl", out var e4) && e4.ValueKind == System.Text.Json.JsonValueKind.String)
                        iconUrl = e4.GetString();
                    else if (root.TryGetProperty("IconUrl", out var e42) && e42.ValueKind == System.Text.Json.JsonValueKind.String)
                        iconUrl = e42.GetString();

                    if (chatId != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB PARSED] GroupUpdated -> chatId: {chatId}, name: {name}, iconUrl: {iconUrl}");
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => GroupUpdated?.Invoke(chatId, name, description, iconUrl)));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed to parse GroupUpdated payload: {ex.Message}");
                }
            });

            conn.On<object>("UserStatusChanged", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    System.Diagnostics.Debug.WriteLine($"[HUB RAW] UserStatusChanged payload: {json}");

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var statusData = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);

                    if (statusData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB PARSED] UserStatusChanged -> userId: {statusData.UserId}, isOnline: {statusData.IsOnline}, lastSeen: {statusData.LastSeen}");

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UserStatusChanged?.Invoke(statusData.UserId, statusData.IsOnline, statusData.LastSeen);
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICES] Failed to parse UserStatusChanged payload: {ex.Message}");
                }
            });

            conn.On<object>("ProfileUpdated", (data) => // ДОДАНО
            {
                try
                {
                    // Normalize incoming payload: it might be an anonymous object or a POCO with different casing
                    string json;
                    if (data is string s)
                    {
                        json = s;
                    }
                    else
                    {
                        json = System.Text.Json.JsonSerializer.Serialize(data);
                    }

                    System.Diagnostics.Debug.WriteLine($"[HUB RAW] ProfileUpdated payload: {json}");

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    ProfileUpdateDto? profileData = null;
                    try { profileData = System.Text.Json.JsonSerializer.Deserialize<ProfileUpdateDto>(json, options); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] ProfileUpdated deserialize failed: {ex.Message}"); }

                    // If direct deserialization failed, try to parse as JsonDocument to locate properties
                    if (profileData == null)
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            int uid = 0;
                            string pfp = null;

                            if (root.TryGetProperty("userId", out var e1) && e1.ValueKind == System.Text.Json.JsonValueKind.Number)
                                uid = e1.GetInt32();
                            else if (root.TryGetProperty("UserId", out var e12) && e12.ValueKind == System.Text.Json.JsonValueKind.Number)
                                uid = e12.GetInt32();

                            if (root.TryGetProperty("pfpUrl", out var e2) && e2.ValueKind == System.Text.Json.JsonValueKind.String)
                                pfp = e2.GetString();
                            else if (root.TryGetProperty("PfpUrl", out var e22) && e22.ValueKind == System.Text.Json.JsonValueKind.String)
                                pfp = e22.GetString();

                            if (uid != 0)
                                profileData = new ProfileUpdateDto { UserId = uid, PfpUrl = pfp };
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse ProfileUpdated payload as JsonDocument: {ex}");
                        }
                    }

                    if (profileData != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB PARSED] ProfileUpdated -> userId: {profileData.UserId}, pfp: {profileData.PfpUrl}");

                        try
                        {
                            // Fire on UI thread asynchronously
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => ProfileUpdated?.Invoke(profileData.UserId, profileData.PfpUrl)));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed to invoke ProfileUpdated event: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed to parse ProfileUpdated payload: {ex.Message}");
                }
            });

            // Attach lifecycle handlers that know which connection raised the event
            conn.Closed += async (ex) => await OnConnectionClosedInternal(conn, ex);
            conn.Reconnecting += (ex) => OnReconnectingInternal(conn, ex);
            conn.Reconnected += (id) => OnReconnectedInternal(conn, id);
        }

        private void RegisterCallHandlers(HubConnection conn)
        {
            if (conn == null) return;

            System.Diagnostics.Debug.WriteLine($"[HUB] RegisterCallHandlers called for connection (State={conn.State})");

            lock (_stateLock)
            {
                if (_callHandlersRegisteredSet.Contains(conn))
                {
                    System.Diagnostics.Debug.WriteLine("[HUB] Call handlers already registered for this connection instance");
                    return;
                }
                _callHandlersRegisteredSet.Add(conn);
            }

            conn.On<object>("IncomingCall", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    System.Diagnostics.Debug.WriteLine($"[HUB][IncomingCall RAW] payload: {json}");
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var incoming = System.Text.Json.JsonSerializer.Deserialize<IncomingCallData>(json, options);
                    if (incoming != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB] Parsed IncomingCall -> CallId={incoming.CallId}, ChatId={incoming.ChatId}, Initiator={incoming.InitiatorId}");
                        Application.Current.Dispatcher.Invoke(() => IncomingCallReceived?.Invoke(incoming));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[HUB] IncomingCall deserialized to null");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed to parse IncomingCall (call connection): {ex}");
                }
            });

            // New: Calling event (initiator) - payload contains CallId and CallUid
            conn.On<object>("Calling", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    int callId = 0;
                    string? callUid = null;
                    // Try both camelCase and PascalCase property names to be robust
                    System.Text.Json.JsonElement e1, e2;
                    if (root.TryGetProperty("callId", out e1) || root.TryGetProperty("CallId", out e1))
                        if (e1.ValueKind == System.Text.Json.JsonValueKind.Number) callId = e1.GetInt32();
                    if (root.TryGetProperty("callUid", out e2) || root.TryGetProperty("CallUid", out e2))
                        if (e2.ValueKind == System.Text.Json.JsonValueKind.String) callUid = e2.GetString();

                    if (callId != 0)
                    {
                        Application.Current.Dispatcher.Invoke(() => CallingReceived?.Invoke(callId, callUid));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed to parse Calling payload: {ex}");
                }
            });

            conn.On<object>("CallAccepted", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<CallSimpleEvent>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => CallAcceptedReceived?.Invoke(d.CallId, d.UserId));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse CallAccepted payload: {ex}"); }
            });

            conn.On<object>("CallRejected", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<CallRejectedEvent>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => CallRejectedReceived?.Invoke(d.CallId, d.UserId, d.Reason));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse CallRejected payload: {ex}"); }
            });

            conn.On<object>("CallEnded", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<CallSimpleEvent>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => CallEndedReceived?.Invoke(d.CallId, d.UserId));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse CallEnded payload: {ex}"); }
            });

            conn.On<object>("Offer", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<SignalData>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => OfferReceived?.Invoke(d));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse Offer payload: {ex}"); }
            });

            conn.On<object>("Answer", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<SignalData>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => AnswerReceived?.Invoke(d));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse Answer payload: {ex}"); }
            });

            conn.On<object>("IceCandidate", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var d = System.Text.Json.JsonSerializer.Deserialize<SignalIce>(json, options);
                    if (d != null) Application.Current.Dispatcher.Invoke(() => IceCandidateReceived?.Invoke(d));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse IceCandidate payload: {ex}"); }
            });

            // New AudioChunk handler includes sequence and timestamp
            conn.On<int, byte[], int, long, long>("AudioChunk", (fromUserId, chunk, callId, sequenceId, timestampMs) =>
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() => AudioChunkReceived?.Invoke(fromUserId, chunk, callId, sequenceId, timestampMs));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] Failed handling AudioChunk: {ex.Message}");
                }
            });

            // also hook lifecycle events for call connection but use connection-aware handlers
            conn.Closed += async (ex) => await OnConnectionClosedInternal(conn, ex);
            conn.Reconnecting += (ex) => OnReconnectingInternal(conn, ex);
            conn.Reconnected += (id) => OnReconnectedInternal(conn, id);
        }

        // Unregister handlers to avoid duplicate invocation when connections are replaced
        private void UnregisterHandlers(HubConnection? conn)
        {
            if (conn == null) return;
            try
            {
                // Remove by method name; ignore failures
                conn.Remove("ReceiveMessage");
                conn.Remove("ReceiveMessageUpdate");
                conn.Remove("TryNotifyReminding");
                conn.Remove("ReceiveMessageDelete");
                conn.Remove("GroupCreated");
                conn.Remove("GroupUpdated");
                conn.Remove("UserStatusChanged");
                conn.Remove("ProfileUpdated");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to unregister handlers: {ex}"); }

            lock (_stateLock)
            {
                try { _handlersRegisteredSet.Remove(conn); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to remove connection from registered handlers: {ex}"); }
            }
        }

        private void UnregisterCallHandlers(HubConnection? conn)
        {
            if (conn == null) return;
            try
            {
                conn.Remove("IncomingCall");
                conn.Remove("Calling");
                conn.Remove("CallAccepted");
                conn.Remove("CallRejected");
                conn.Remove("CallEnded");
                conn.Remove("Offer");
                conn.Remove("Answer");
                conn.Remove("IceCandidate");
                conn.Remove("AudioChunk");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to unregister call handlers: {ex}"); }

            lock (_stateLock)
            {
                try { _callHandlersRegisteredSet.Remove(conn); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Failed to remove connection from registered call handlers: {ex}"); }
            }
        }

        private async Task OnConnectionClosed(Exception? error)
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

        private Task OnReconnecting(Exception? error)
        {
            if (!_allowReconnect)
            {
                return Task.CompletedTask;
            }

            _isReconnecting = true;
            OnConnectionStateChanged(false);
            return Task.CompletedTask;
        }

        private Task OnReconnected(string? connectionId)
        {
            if (!_allowReconnect)
            {
                _ = DisconnectAsync();
                return Task.CompletedTask;
            }

            _isReconnecting = false;
            OnConnectionStateChanged(true);
            return Task.CompletedTask;
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    // No dispatcher available - invoke directly
                    ConnectionStateChanged?.Invoke(isConnected);
                    return;
                }

                // If dispatcher is shutting down, avoid Invoke/BeginInvoke
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    ConnectionStateChanged?.Invoke(isConnected);
                    return;
                }

                if (dispatcher.CheckAccess())
                {
                    ConnectionStateChanged?.Invoke(isConnected);
                }
                else
                {
                    // Use BeginInvoke to avoid blocking calling thread and avoid TaskCanceledException
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            ConnectionStateChanged?.Invoke(isConnected);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] ConnectionStateChanged handler error: {ex.Message}");
                        }
                    }));
                }
            }
            catch (TaskCanceledException)
            {
                // Dispatcher is being shut down - ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] OnConnectionStateChanged failed: {ex.Message}");
            }
        }

        private void ShowError(string title, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                uchat.Pages.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _allowReconnect = false;
            _handlersRegisteredSet.Clear();
            _callHandlersRegisteredSet.Clear();

            StopConnectionCheckTimer();
            // Run disconnect on threadpool to avoid blocking UI thread
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB] Dispose DisconnectAsync failed: {ex}");
                    }
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] Dispose failed: {ex.Message}"); }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private async Task ShowToast(MessageDto content)
        {
            try
            {
                // Підготовка даних
                var sender = await App.ApiService.GetUserByIdAsync(content.SenderId);
                string senderName = sender?.Username ?? "Невідомий";
                string messageText = content.Text;
                string msg = messageText.Length > 100 ? senderName + ":" + messageText.Substring(0, 100) + "..." : senderName + ":" + messageText;
                string title = "Нове повідомлення";
                var builder = new ToastContentBuilder()
                .AddArgument("action", "viewChat")
                .AddArgument("chatId", content.ChatId.ToString())
                .AddText(title, AdaptiveTextStyle.Title)
                .AddText(msg);

                builder.Show(toast =>
                {
                    toast.Tag = content.Id.ToString();
                    toast.Group = "chatNotifications";
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(3);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка показу Toast: {ex}");
            }
        }

        private async Task ShowReminderToast(int remindingId)
        {
            try
            {
                string title = "Reminding deadline reached!";
                string msg = $"Please check your tasks!";
                var builder = new ToastContentBuilder()
                    .AddArgument("action", "viewReminding")
                    .AddArgument("remindingId", remindingId.ToString())
                    .AddText(title, AdaptiveTextStyle.Title).AddText(msg);
                builder.Show(toast => {
                    toast.Tag = remindingId.ToString();
                    toast.Group = "reminderNotifications";
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(3);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show reminder toast: {ex}");
            }
        }

        /// <summary>
        /// Запитати статус користувача з сервера (синхронний запит через Hub)
        /// </summary>
        public async Task<object?> QueryUserStatusAsync(int userId)
        {
            if (!IsConnected || _connection == null)
                return null;

            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                // Pass the argument directly to ensure SignalR binds it to the server method parameter correctly
                var result = await _connection.InvokeAsync<object>("GetUserStatus", userId, cts.Token);
                if (result == null) return null;

                var json = System.Text.Json.JsonSerializer.Serialize(result);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var statusData = System.Text.Json.JsonSerializer.Deserialize<UserStatusDto>(json, options);
                return statusData as object;
            }
            catch (Exception ex)
            {
                // Some HubException types may come from server or client assemblies not referenced here.
                // Avoid referencing Microsoft.AspNetCore.SignalR.Client.HubException directly to keep compilation safe.
                try
                {
                    var fullType = ex.GetType()?.FullName ?? ex.GetType()?.Name ?? "<unknown>";
                    System.Diagnostics.Debug.WriteLine($"[HUB SERVICE] QueryUserStatusAsync exception type={fullType}; message={ex.Message}");

                    // If it's a server-side HubException, its runtime type name usually contains 'HubException'
                    if ((ex.GetType()?.Name ?? string.Empty).IndexOf("HubException", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[HUB SERVICE] Server-side HubException: " + ex.Message);
                    }

                    var msg = ex.Message ?? string.Empty;
                    if (msg.IndexOf("Connection closed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("closed the connection", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _ = OnConnectionClosed(ex);
                    }
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB] Failed to log QueryUserStatusAsync error: {logEx}");
                }

                return null;
            }
        }

        // Public client-side wrappers to call hub methods
        public async Task StartCallAsync(int chatId, string callUid, object? metadata = null)
        {
            // prefer call-specific connection
            // Ensure call-specific connection exists and is connected; require it (no fallback)
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection not available.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

                // ВАРІАНТ 1: Якщо metadata це вже об'єкт (POCO) або JsonElement -> відправляємо як є.
                // SignalR сам серіалізує його в правильний JSON-об'єкт.
                if (metadata is not string)
                {
                    // Використовуємо перевантаження з розкритими параметрами, щоб уникнути помилок масиву object[]
                    await conn.InvokeAsync("StartCall", chatId, callUid, metadata, cts.Token);
                }
                else
                {
                    // ВАРІАНТ 2: Якщо metadata це рядок (raw JSON), нам треба розпарсити його,
                    // щоб сервер отримав його як структуру (JsonElement), а не як рядок.
                    var jsonString = (string)metadata;
                    try
                    {
                        // Парсимо рядок у JsonDocument. 
                        // Using важливий, щоб очистити пам'ять, але він має жити до завершення InvokeAsync
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonString);

                        // Відправляємо RootElement (це і є JsonElement)
                        await conn.InvokeAsync("StartCall", chatId, callUid, doc.RootElement, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HUB] Failed to parse metadata JSON for StartCall: {ex}");
                        // Якщо рядок не є валідним JSON, відправляємо як звичайний рядок
                        await conn.InvokeAsync("StartCall", chatId, callUid, jsonString, cts.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] StartCall failed: {ex.Message}");
            }
        }

        public async Task AcceptCallAsync(int callId)
        {
            // require call-specific connection; do not fallback to main connection
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await conn.InvokeAsync("AcceptCall", callId, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] AcceptCall failed: {ex.Message}");
            }
        }

        public async Task RejectCallAsync(int callId, string? reason = null)
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await conn.InvokeAsync("RejectCall", callId, reason, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] RejectCall failed: {ex.Message}");
            }
        }

        public async Task EndCallAsync(int callId)
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                // Use SendAsync (fire-and-forget) instead of InvokeAsync to avoid waiting for a server response
                // which may time out and throw TaskCanceledException under flaky networks.
                var sendTask = conn.SendAsync("EndCall", callId);
                sendTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        try
                        {
                            var ex = t.Exception?.GetBaseException();
                            System.Diagnostics.Debug.WriteLine($"[HUB] EndCall SendAsync failed: {ex?.Message}");
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] EndCall SendAsync continuation failed: {ex.Message}"); }
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (TaskCanceledException tce)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] EndCall canceled: {tce.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] EndCall failed: {ex.Message}");
            }
        }

        public async Task SendOfferAsync(int targetUserId, string sdp, string callUid)
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await conn.InvokeAsync("SendOffer", targetUserId, sdp, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendOffer failed: {ex.Message}");
            }
        }

        public async Task SendAnswerAsync(int targetUserId, string sdp, string callUid)
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await conn.InvokeAsync("SendAnswer", targetUserId, sdp, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendAnswer failed: {ex.Message}");
            }
        }

        public async Task SendIceCandidateAsync(int targetUserId, string candidate, string? sdpMid, int? sdpMLineIndex, string callUid)
        {
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await conn.InvokeAsync("SendIceCandidate", targetUserId, candidate, sdpMid, sdpMLineIndex, callUid, cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendIceCandidate failed: {ex.Message}");
            }
        }

        // Send audio with sequence/timestamp for jitter/ordering
        public async Task SendAudioChunkAsync(int? targetUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            // Make this fire-and-forget to reduce latency for audio streaming. Attach continuation to log faults.
            if (!await EnsureCallConnectionAsync())
            {
                System.Diagnostics.Debug.WriteLine("[HUB] Call connection unavailable.");
                return;
            }
            var conn = _callConnection;
            if (conn == null || conn.State != HubConnectionState.Connected) return;
            try
            {
                var sendTask = conn.SendAsync("SendAudioChunk", targetUserId, chunk, callId, sequenceId, timestampMs);
                // Observe faulted task to log errors
                sendTask.ContinueWith(t =>
                {
                    try
                    {
                        var ex = t.Exception?.GetBaseException();
                        System.Diagnostics.Debug.WriteLine($"[HUB] SendAudioChunk failed: {ex?.Message}");
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HUB] SendAudioChunk continuation failed: {ex.Message}"); }
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HUB] SendAudioChunkAsync failed: {ex.Message}");
            }
        }

        // small helper to pick call connection
        private HubConnection? _call_connection_or_fallback() => _callConnection; // no fallback to main hub

        // Helper: try to start a HubConnection with a few retries, logging progress
        private async Task<bool> TryStartWithRetriesAsync(HubConnection? conn, string tag)
        {
            if (conn == null) return false;
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[HUB][{tag}] Start attempt {attempt}...");
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
    }
}