using Edemly.Client.Helpers;
using Edemly.Client.Realtime.Notifications;
using Edemly.Client.Services;
using Edemly.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows;

namespace Edemly.Client.Realtime
{
    public partial class HubService : IHubService
    {
        private HubConnection? _connection;
        private HubConnection? _callConnection;
        private readonly string _serverUrl;
        private bool _disposed;
        private Timer? _connectionCheckTimer;
        private bool _isReconnecting;

        private bool _allowReconnect = true;
        private readonly System.Collections.Generic.HashSet<HubConnection> _handlersRegisteredSet = new System.Collections.Generic.HashSet<HubConnection>();
        private readonly System.Collections.Generic.HashSet<HubConnection> _callHandlersRegisteredSet = new System.Collections.Generic.HashSet<HubConnection>();
        private readonly object _stateLock = new object();
        private string? _lastAccessToken;

        public event Action<MessageDto>? MessageReceived;

        public event Action<MessageDto>? MessageUpdated;

        public event Action<int, int>? MessageDeleted;

        public event Action<bool>? ConnectionStateChanged;

        public event Action<int>? GroupCreated;

        public event Action<int, string?, string?, string?>? GroupUpdated; // chatId, name, description, iconUrl

        public event Action<int, bool, DateTime?>? UserStatusChanged;

        public event Action<int, string>? ProfileUpdated; // ДОДАНО

        public event Action<IncomingCallEventDto>? IncomingCallReceived;

        public event Action<int, int>? CallAcceptedReceived; // callId, userId

        public event Action<int, int, string?>? CallRejectedReceived; // callId, userId, reason

        public event Action<int, int>? CallEndedReceived; // callId, userId

        public event Action<SignalDataDto>? OfferReceived;

        public event Action<SignalDataDto>? AnswerReceived;

        public event Action<SignalIceDto>? IceCandidateReceived;

        public event Action<int, string?>? CallingReceived; // callId, callUid

        public event Action<int, byte[], int, long, long>? AudioChunkReceived; // fromUserId, chunk, callId, sequenceId, timestampMs

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        public bool IsCallConnected => _callConnection?.State == HubConnectionState.Connected;

        public bool IsReconnecting => _isReconnecting;

        private readonly ToastNotificationService _toastNotificationService = new();
        private readonly ReminderNotificationService _reminderNotificationService = new();

        private string BuildHubUrl(string hubName)
        {
            return UrlHelper.BuildHubUrl(
                _serverUrl,
                hubName,
                ConfigService.Instance?.Company);
        }

        public HubService(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            _serverUrl = UrlHelper.NormalizeBaseUrl(serverUrl);
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                {
                    ConnectionStateChanged?.Invoke(isConnected);
                    return;
                }

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
                Edemly.Client.Pages.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _allowReconnect = false;
            _handlersRegisteredSet.Clear();
            _callHandlersRegisteredSet.Clear();

            StopConnectionCheckTimer();
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
    }
}