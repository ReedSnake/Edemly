using Concentus.Structs;
using Edemly.Client.Application.Localization;
using Edemly.Client.Infrastructure.Realtime; // ? ���������
using Edemly.Client.Presentation.Common;
using Edemly.Contracts.Realtime;
using NAudio.Wave;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Edemly.Client.Presentation.Windows.Calls
{
    internal class LoopStream : WaveStream
    {
        private readonly WaveStream _sourceStream;

        public LoopStream(WaveStream sourceStream)
        { _sourceStream = sourceStream; }

        public override WaveFormat WaveFormat => _sourceStream.WaveFormat;
        public override long Length => long.MaxValue;
        public override long Position { get => _sourceStream.Position; set => _sourceStream.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = _sourceStream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    _sourceStream.Position = 0;
                }
                totalRead += read;
            }
            return totalRead;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _sourceStream.Dispose();
        }
    }

public partial class CallWindow : ThemedWindow
    {
        private readonly HubService _hub;
        private bool _inCall = false;
        private int? _currentCallId = null;
        private string? _currentCallUid = null;
        private int? _callInitiatorId = null;
        private int? _currentChatId = null;

        private WaveInEvent? _waveIn;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private WaveOutEvent? _waveOut;
        private bool _muted = false;
        private int? _peerUserId = null; 

        private WaveOutEvent? _ringWaveOut;
        private LoopStream? _ringLoopStream;
        private string? _ringTonePath;

        private WaveOutEvent? _endCallWaveOut;
        private WaveFileReader? _endCallReader;
        private string? _endCallSoundPath;

        private readonly object _jitterLock = new object();
        private Dictionary<int, SortedDictionary<long, byte[]>> _jitterBuffers = new Dictionary<int, SortedDictionary<long, byte[]>>();
        private System.Threading.Timer? _playoutTimer;
        private long _sendSequence = 0;
        private Dictionary<int, bool> _participantMuted = new Dictionary<int, bool>();

        private System.Threading.Timer? _noPeerTimer;
        private System.Threading.Timer? _dialCountdownTimer;

        private int _dialSecondsLeft = 30;
        private int? _dialCallId = null;

        private const double AGC_TARGET_RMS = 0.05;
        private const double AGC_SMOOTHING = 0.15;
        private const double AGC_MAX_GAIN = 8.0;
        private const double NOISE_GATE_THRESHOLD = 0.002;

        private DispatcherTimer? _notificationTimer;

        private bool _hubHandlersRegistered = false;

        public CallWindow()
        {
            InitializeComponent();
            _hub = App.HubService as HubService ?? throw new InvalidOperationException("HubService not available");

            try
            {
                _ringTonePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "call-ringtone.wav");
                if (!System.IO.File.Exists(_ringTonePath)) _ringTonePath = null;
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to determine ringTonePath: {ex}"); _ringTonePath = null; }

            try
            {
                _endCallSoundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "call-ended.wav");
                if (!System.IO.File.Exists(_endCallSoundPath)) _endCallSoundPath = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALLWINDOW] Failed to determine endCallSoundPath: {ex}"); _endCallSoundPath = null;
            }

            _ = LoadActiveCallsAsync();
        }

        private static void SetThemeResource(FrameworkElement element, DependencyProperty property, string resourceKey)
        {
            element.SetResourceReference(property, resourceKey);
        }

        public void RegisterHubHandlers()
        {
            if (_hub == null) return;
            if (_hubHandlersRegistered) return;

            _hub.CallAcceptedReceived += OnCallAccepted;
            _hub.CallRejectedReceived += OnCallRejected;
            _hub.CallEndedReceived += OnCallEnded;

            _hub.AudioChunkReceived += OnAudioChunkReceived;
            _hub.CallingReceived += OnCallingReceived;

            _hubHandlersRegistered = true;
            Debug.WriteLine("[CALLWINDOW] Hub handlers registered");
        }

        public void UnregisterHubHandlers()
        {
            if (_hub == null) return;
            if (!_hubHandlersRegistered) return;

            try { _hub.CallAcceptedReceived -= OnCallAccepted; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallAcceptedReceived: {ex}"); }
            try { _hub.CallRejectedReceived -= OnCallRejected; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallRejectedReceived: {ex}"); }
            try { _hub.CallEndedReceived -= OnCallEnded; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallEndedReceived: {ex}"); }
            try { _hub.AudioChunkReceived -= OnAudioChunkReceived; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister AudioChunkReceived: {ex}"); }
            try { _hub.CallingReceived -= OnCallingReceived; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallingReceived: {ex}"); }

            _hubHandlersRegistered = false;
            Debug.WriteLine("[CALLWINDOW] Hub handlers unregistered");
        }

        private void ShowInlineNotification(string message, int seconds = 3)
        {
            try
            {
                NotificationText.Text = message;
                NotificationBorder.Visibility = Visibility.Visible;

                _notificationTimer?.Stop();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    NotificationBorder.Visibility = Visibility.Collapsed;
                };
                _notificationTimer = timer;
                _notificationTimer.Start();
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] ShowInlineNotification failed: {ex}"); }
        }

        private void PlayEndSound()
        {
            try
            {
                if (string.IsNullOrEmpty(_endCallSoundPath)) return;
                try { _endCallWaveOut?.Stop(); _endCallWaveOut?.Dispose(); _endCallWaveOut = null; _endCallReader?.Dispose(); _endCallReader = null; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] PlayEndSound cleanup failed: {ex}"); }
                _endCallReader = new WaveFileReader(_endCallSoundPath);
                _endCallWaveOut = new WaveOutEvent();
                _endCallWaveOut.Init(_endCallReader);
                _endCallWaveOut.Play();
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] PlayEndSound failed: {ex}"); }
        }

        private void PlayRingtone()
        {
            try
            {
                if (string.IsNullOrEmpty(_ringTonePath)) return;
                try { _ringWaveOut?.Stop(); _ringWaveOut?.Dispose(); _ringWaveOut = null; _ringLoopStream?.Dispose(); _ringLoopStream = null; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] PlayRingtone cleanup failed: {ex}"); }
                var reader = new WaveFileReader(_ringTonePath);
                _ringLoopStream = new LoopStream(reader);
                _ringWaveOut = new WaveOutEvent();
                _ringWaveOut.Init(_ringLoopStream);
                _ringWaveOut.Play();
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] PlayRingtone failed: {ex}"); }
        }

        private void StopRingtone()
        {
            try
            {
                try { _ringWaveOut?.Stop(); _ringWaveOut?.Dispose(); _ringWaveOut = null; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopRingtone inner stop failed: {ex}"); }
                try { _ringLoopStream?.Dispose(); _ringLoopStream = null; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopRingtone inner dispose failed: {ex}"); }
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopRingtone outer failed: {ex}"); }
        }

        private async Task LoadActiveCallsAsync()
        {
            CallsListPanel.Children.Clear();
            try
            {
                var api = App.ApiClients;
                var calls = await api.Calls.GetActiveCallsAsync();
                if (calls == null || calls.Count == 0)
                {
                    var noCallsText = new TextBlock { Text = DefaultLanguage.NoActiveCall, Margin = new Thickness(6) };
                    SetThemeResource(noCallsText, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                    CallsListPanel.Children.Add(noCallsText);
                    return;
                }

                foreach (var c in calls)
                {
                    var container = new Border
                    {
                        Margin = new Thickness(4),
                        Padding = new Thickness(6),
                        CornerRadius = new CornerRadius(6),
                        BorderThickness = new Thickness(1)
                    };
                    SetThemeResource(container, Border.BackgroundProperty, "ThemeSurfaceBrush");
                    SetThemeResource(container, Border.BorderBrushProperty, "ThemeBorderBrush");
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var avatarBorder = new Border
                    {
                        Width = 40,
                        Height = 40,
                        CornerRadius = new CornerRadius(20),
                        VerticalAlignment = VerticalAlignment.Center,
                        BorderThickness = new Thickness(1)
                    };
                    SetThemeResource(avatarBorder, Border.BackgroundProperty, "ThemeSurfaceAltBrush");
                    SetThemeResource(avatarBorder, Border.BorderBrushProperty, "ThemeBorderLightBrush");
                    var avatarImg = new Image { Width = 40, Height = 40, Stretch = System.Windows.Media.Stretch.UniformToFill };
                    avatarBorder.Child = avatarImg;
                    Grid.SetColumn(avatarBorder, 0);
                    grid.Children.Add(avatarBorder);

                    var info = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    var title = new TextBlock { Text = c.CallUid ?? $"Call {c.Id}", FontWeight = FontWeights.SemiBold };
                    var subtitle = new TextBlock { Text = $"Chat {c.ChatId}", FontSize = 12, Opacity = 0.8 };
                    SetThemeResource(title, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
                    SetThemeResource(subtitle, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                    info.Children.Add(title); info.Children.Add(subtitle);
                    Grid.SetColumn(info, 1);
                    grid.Children.Add(info);

                    var joinBtn = new Button { Content = "Join", Tag = new { c.Id, c.CallUid, c.ChatId, c.InitiatorId }, Margin = new Thickness(6, 0, 0, 0) };
                    if (TryFindResource("SecondaryButtonStyle") is Style secondaryButtonStyle)
                    {
                        joinBtn.Style = secondaryButtonStyle;
                    }
                    joinBtn.Click += async (s, e) =>
                    {
                        var tag = (dynamic)joinBtn.Tag;
                        await JoinCallAsync((int)tag.Id, (string)tag.CallUid, (int)tag.ChatId, (int)tag.InitiatorId);
                    };
                    Grid.SetColumn(joinBtn, 2);
                    grid.Children.Add(joinBtn);

                    container.Child = grid;
                    CallsListPanel.Children.Add(container);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var user = await App.ApiClients.Users.GetUserByIdAsync(c.InitiatorId);
                            if (user == null) return;

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                title.Text = user.Username ?? title.Text;

                                if (!string.IsNullOrEmpty(user.PfpUrl))
                                {
                                    var bmp = await App.GlobalProfilePictureCache.GetOrDownloadAsync(user.PfpUrl);
                                    if (bmp != null)
                                    {
                                        avatarImg.Source = bmp;
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CALLWINDOW] LoadActiveCalls user fetch failed: {ex}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock { Text = $"{DefaultLanguage.Loading} {ex.Message}", Margin = new Thickness(6), TextWrapping = TextWrapping.Wrap };
                SetThemeResource(errorText, TextBlock.ForegroundProperty, "ThemeDangerBrush");
                CallsListPanel.Children.Add(errorText);
            }
        }

        private async Task LeavePreviousCallIfAnyAsync()
        {
            try
            {
                if (_inCall && _currentCallId.HasValue)
                {
                    try { await _hub.EndCallAsync(_currentCallId.Value); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] LeavePreviousCall EndCall failed: {ex}"); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] LeavePreviousCallIfAnyAsync failed: {ex}"); }
            finally
            {
                StopAudio();
                _inCall = false;
                _currentCallId = null; _currentCallUid = null; _callInitiatorId = null; _currentChatId = null;
            }
        }

        private async Task JoinCallAsync(int callId, string callUid, int chatId, int initiatorId)
        {
            if (_inCall && _currentCallId != callId) await LeavePreviousCallIfAnyAsync();
            if (_inCall) return;

            _inCall = true; _currentCallId = callId; _currentCallUid = callUid; _callInitiatorId = initiatorId; _currentChatId = chatId;

            ListViewGrid.Visibility = Visibility.Collapsed;
            CallViewGrid.Visibility = Visibility.Visible;
            CallTitle.Text = DefaultLanguage.IncomingCall; // ? ������������ - "In call"
            CallInfoText.Text = string.Format(DefaultLanguage.Connecting, callId); // ? ������������

            StopRingtone();
            await _hub.AcceptCallAsync(callId);

            try
            {
                var members = await App.ApiClients.Chats.GetChatMembersAsync(chatId);
                var me = App.CurrentUserId ?? 0;
                var peer = members?.FirstOrDefault(m => m.UserId != me);
                if (peer != null) _peerUserId = peer.UserId;

                if (members != null)
                {
                    foreach (var m in members)
                    {
                        _participantMuted[m.UserId] = false;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] JoinCall member fetch failed: {ex}"); }

            StartAudio();
            _noPeerTimer?.Dispose(); _noPeerTimer = null;
        }

        private void StartAudio()
        {
            try
            {
                if (_currentCallId == null) return;

                _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(16000, 16, 1)) { DiscardOnBufferOverflow = true };
                _waveOut = new WaveOutEvent(); _waveOut.Init(_bufferedWaveProvider); _waveOut.Play();

                lock (_jitterLock)
                {
                    _jitterBuffers.Clear();
                }

                _playoutTimer?.Dispose(); _playoutTimer = null;

                _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                _waveIn.DataAvailable += (s, a) =>
                {
                    try
                    {
                        if (_muted) return;
                        var bytes = new byte[a.BytesRecorded]; Array.Copy(a.Buffer, 0, bytes, 0, a.BytesRecorded);

                        var sequence = Interlocked.Increment(ref _sendSequence);
                        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _hub.SendAudioChunkAsync(null, bytes, _currentCallId.Value, sequence, timestampMs);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[CALL] SendAudioChunk failed: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CALL] Audio capture handler error: {ex.Message}");
                    }
                };
                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                ShowInlineNotification($"{DefaultLanguage.CallFailed}: {ex.Message}"); // ? ������������
            }
        }

        private void StopAudio()
        {
            try
            {
                if (_waveIn != null) { try { _waveIn.StopRecording(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopAudio waveIn stop failed: {ex}"); } _waveIn.Dispose(); _waveIn = null; }
                if (_waveOut != null) { try { _waveOut.Stop(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopAudio waveOut stop failed: {ex}"); } _waveOut.Dispose(); _waveOut = null; }
                _bufferedWaveProvider = null; _peerUserId = null; _inCall = false;

                _playoutTimer?.Dispose(); _playoutTimer = null;
                lock (_jitterLock) { _jitterBuffers.Clear(); }
                _participantMuted.Clear();
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopAudio failed: {ex}"); }
        }

        private void OnAudioChunkReceived(int fromUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            try
            {
                if (_currentCallId == null) return;
                if (callId != _currentCallId.Value) return;

                if (App.CurrentUserId.HasValue && fromUserId == App.CurrentUserId.Value) return;

                if (_participantMuted.TryGetValue(fromUserId, out var isMuted) && isMuted) return;

                try
                {
                    if (chunk != null && chunk.Length > 0 && _bufferedWaveProvider != null)
                    {
                        _bufferedWaveProvider.AddSamples(chunk, 0, chunk.Length);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CALL] Playback error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CALL] OnAudioChunkReceived error: {ex.Message}");
            }
        }

        public void HandleIncomingCall(IncomingCallEventDto? data)
        {
            if (data is not { } incomingCall)
            {
                Debug.WriteLine("[CALLWINDOW] Incoming call data is null");
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Debug.WriteLine(
                        $"[CALLWINDOW] HandleIncomingCall invoked. callId={incomingCall.CallId} callUid={incomingCall.CallUid} metadata={incomingCall.Metadata}");

                    if (_inCall && _currentCallId.HasValue && _currentCallId.Value == incomingCall.CallId)
                    {
                        try
                        {
                            if (!IsVisible)
                            {
                                Owner = System.Windows.Application.Current.MainWindow;
                                Show();
                            }

                            WindowState = WindowState.Normal;
                            Topmost = true;
                            Topmost = false;
                            Activate();

                            Debug.WriteLine("[CALLWINDOW] Already in call - window brought to front");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CALLWINDOW] Failed to bring window to front: {ex}");
                        }

                        return;
                    }

                    if (_inCall && _currentCallId.HasValue && _currentCallId.Value != incomingCall.CallId)
                    {
                        ShowInlineNotification("Incoming call received while already in a call");
                        Debug.WriteLine("[CALLWINDOW] Incoming call ignored because already in another call");
                        return;
                    }

                    IncomingText.Text = DefaultLanguage.IncomingCall;
                    IncomingFromText.Text = $"{DefaultLanguage.IncomingCall}: {incomingCall.InitiatorId}";

                    try
                    {
                        Panel.SetZIndex(IncomingPrompt, 9999);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] SetZIndex failed: {ex}");
                    }

                    try
                    {
                        ListViewGrid.Visibility = Visibility.Collapsed;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] Hide ListViewGrid failed: {ex}");
                    }

                    IncomingPrompt.Visibility = Visibility.Visible;

                    try
                    {
                        Owner = System.Windows.Application.Current.MainWindow;

                        if (!IsVisible)
                        {
                            Show();
                        }

                        WindowState = WindowState.Normal;
                        Topmost = true;
                        Topmost = false;
                        Activate();

                        Debug.WriteLine("[CALLWINDOW] Incoming prompt shown and window activated");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] Failed to show/activate window: {ex}");
                    }

                    PlayRingtone();

                    _currentCallId = incomingCall.CallId;
                    _currentCallUid = incomingCall.CallUid;
                    _callInitiatorId = incomingCall.InitiatorId;
                    _currentChatId = incomingCall.ChatId;

                    try
                    {
                        var btn = new Button
                        {
                            Content = $"Incoming: {incomingCall.CallUid} from {incomingCall.InitiatorId}",
                            Tag = new
                            {
                                incomingCall.CallId,
                                incomingCall.CallUid,
                                incomingCall.ChatId,
                                incomingCall.InitiatorId
                            },
                            Margin = new Thickness(4)
                        };
                        if (TryFindResource("PrimaryButtonStyle") is Style primaryButtonStyle)
                        {
                            btn.Style = primaryButtonStyle;
                        }

                        btn.Click += async (s, e) =>
                        {
                            var tag = (dynamic)btn.Tag;

                            await JoinCallAsync(
                                (int)tag.CallId,
                                (string?)tag.CallUid ?? string.Empty,
                                (int)tag.ChatId,
                                (int)tag.InitiatorId);
                        };

                        CallsListPanel.Children.Insert(0, btn);

                        Debug.WriteLine("[CALLWINDOW] Added incoming call button to list");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] Failed to add button to calls list: {ex}");
                    }

                    try
                    {
                        _noPeerTimer?.Dispose();
                        _noPeerTimer = new System.Threading.Timer(async _ =>
                        {
                            try
                            {
                                if (!_inCall && _currentCallId == incomingCall.CallId)
                                {
                                    StopRingtone();

                                    try
                                    {
                                        await _hub.EndCallAsync(incomingCall.CallId);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[CALLWINDOW] NoPeer timer EndCall failed: {ex}");
                                    }

                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        IncomingPrompt.Visibility = Visibility.Collapsed;

                                        var toRemove = CallsListPanel.Children
                                            .OfType<Button>()
                                            .FirstOrDefault(b =>
                                            {
                                                try
                                                {
                                                    return ((dynamic)b.Tag).CallId == incomingCall.CallId;
                                                }
                                                catch
                                                {
                                                    return false;
                                                }
                                            });

                                        if (toRemove != null)
                                        {
                                            CallsListPanel.Children.Remove(toRemove);
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[CALLWINDOW] NoPeer inner timer failed: {ex}");
                            }
                        }, null, TimeSpan.FromSeconds(30), System.Threading.Timeout.InfiniteTimeSpan);

                        Debug.WriteLine("[CALLWINDOW] No-peer timer started");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] Failed to start no-peer timer: {ex}");
                    }

                    _ = LoadIncomingCallerDetailsAsync(incomingCall.InitiatorId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"HandleIncomingCall UI update error: {ex.Message}");
                }
            });
        }
        private async Task LoadIncomingCallerDetailsAsync(int initiatorId)
        {
            try
            {
                var user = await App.ApiClients.Users.GetUserByIdAsync(initiatorId);
                if (user == null) return;

                var avatar = !string.IsNullOrEmpty(user.PfpUrl)
                    ? await App.GlobalProfilePictureCache.GetOrDownloadAsync(user.PfpUrl)
                    : null;

                await Dispatcher.InvokeAsync(() =>
                {
                    IncomingFromText.Text = $"From: {user.Username}";

                    if (avatar != null)
                    {
                        IncomingAvatar.Source = avatar;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALLWINDOW] Failed to fetch user details: {ex}");
            }
        }
        private void OnCallingReceived(int callId, string? callUid)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DialingPanel.Visibility = Visibility.Visible;
                DialingText.Text = DefaultLanguage.Calling; // ? ����˲������
                _dialSecondsLeft = 30;
                DialingCountdown.Text = _dialSecondsLeft + "s";
                _dialCallId = callId;
                PlayRingtone();

                _dialCountdownTimer?.Dispose();
                _dialCountdownTimer = new System.Threading.Timer(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _dialSecondsLeft--;
                        DialingCountdown.Text = _dialSecondsLeft + "s";

                        if (_dialSecondsLeft <= 0)
                        {
                            _dialCountdownTimer?.Dispose();
                            _dialCountdownTimer = null;
                            DialingPanel.Visibility = Visibility.Collapsed;

                            StopRingtone();

                            if (_dialCallId.HasValue)
                            {
                                _ = _hub.EndCallAsync(_dialCallId.Value);
                                _dialCallId = null;
                            }
                        }
                    });
                }, null, 1000, 1000);
            });
        }

        private async void AcceptIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCallId == null) return;
            IncomingPrompt.Visibility = Visibility.Collapsed;

            StopRingtone();

            await JoinCallAsync(_currentCallId.Value, _currentCallUid ?? string.Empty, _currentChatId ?? 0, _callInitiatorId ?? 0);
        }

        private async void RejectIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCallId == null) return; IncomingPrompt.Visibility = Visibility.Collapsed; StopRingtone(); try { await _hub.RejectCallAsync(_currentCallId.Value, "Rejected by user"); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] RejectIncomingButton error: {ex}"); }
            _currentCallId = null;
        }

        private void OnCallAccepted(int callId, int userId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    if (_dialCallId.HasValue && _dialCallId.Value == callId)
                    {
                        StopRingtone();

                        _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; _dialCallId = null;
                        DialingPanel.Visibility = Visibility.Collapsed;

                        _inCall = true;
                        _currentCallId = callId;
                        CallViewGrid.Visibility = Visibility.Visible;
                        ListViewGrid.Visibility = Visibility.Collapsed;
                        CallTitle.Text = "In call";
                        CallInfoText.Text = $"Call {callId} (connected)";

                        try { _peerUserId = userId; _participantMuted[userId] = false; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Set peer mute state failed: {ex.Message}"); }

                        StartAudio();
                        return;
                    }

                    if (_currentCallId.HasValue && _currentCallId.Value == callId)
                    {
                        if (App.CurrentUserId.HasValue && App.CurrentUserId.Value == userId) return;
                        ShowInlineNotification($"User {userId} joined the call", 3);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CALLWINDOW] OnCallAccepted error: {ex.Message}");
                }
            });
        }

        private void OnCallRejected(int callId, int userId, string? reason)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_currentCallId == callId)
                {
                    StopRingtone();
                    ShowInlineNotification(DefaultLanguage.CallFailed); // ? ����˲������
                    try { _ = _hub.EndCallAsync(callId); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnCallRejected EndCall failed: {ex}"); }
                    StopAudio();
                    PlayEndSound();
                    Close();
                    return;
                }

                if (_dialCallId == callId)
                {
                    StopRingtone();
                    ShowInlineNotification(DefaultLanguage.CallFailed); // ? ����˲������
                    _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; _dialCallId = null; DialingPanel.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void OnCallEnded(int callId, int userId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_currentCallId == callId)
                {
                    StopAudio();
                    PlayEndSound();
                    ShowInlineNotification(DefaultLanguage.CallEnded); // ? ����˲������
                    Close();
                }
            });
        }

        private async void RefreshListButton_Click(object sender, RoutedEventArgs e)
        { await LoadActiveCallsAsync(); }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        { Close(); }

        private async void EndCallButton_Click(object sender, RoutedEventArgs e)
        { if (_currentCallId.HasValue) await _hub.EndCallAsync(_currentCallId.Value); StopAudio(); Close(); }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            _muted = !_muted;
            MuteButton.Content = _muted ? DefaultLanguage.Unmute : DefaultLanguage.Mute; // ? ����˲������
        }

        private async void CancelCallButton_Click(object sender, RoutedEventArgs e)
        {
            DialingPanel.Visibility = Visibility.Collapsed;

            _dialCountdownTimer?.Dispose();
            _dialCountdownTimer = null;

            StopRingtone();

            if (_dialCallId.HasValue)
            {
                try
                {
                    await _hub.EndCallAsync(_dialCallId.Value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CALLWINDOW] CancelCallButton EndCall failed: {ex}");
                }

                _dialCallId = null;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            try { StopRingtone(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing StopRingtone failed: {ex}"); }
            try { StopAudio(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing StopAudio failed: {ex}"); }

            try { UnregisterHubHandlers(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing UnregisterHubHandlers failed: {ex}"); }

            try
            {
                IncomingPrompt.Visibility = Visibility.Collapsed;
                DialingPanel.Visibility = Visibility.Collapsed;
                CallViewGrid.Visibility = Visibility.Collapsed;
                ListViewGrid.Visibility = Visibility.Visible;
                NotificationBorder.Visibility = Visibility.Collapsed;
                _dialCallId = null;
                _dialSecondsLeft = 30;
                _currentCallId = null;
                _currentCallUid = null;
                _callInitiatorId = null;
                _currentChatId = null;
                _inCall = false;
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing reset UI state failed: {ex.Message}"); }

            this.Hide();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try { UnregisterHubHandlers(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosed UnregisterHubHandlers failed: {ex}"); }
            try { StopAudio(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosed StopAudio failed: {ex}"); }
            _noPeerTimer?.Dispose(); _noPeerTimer = null; _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null;
        }
    }
}
