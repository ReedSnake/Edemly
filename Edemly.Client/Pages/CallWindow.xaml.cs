using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using uchat.Services;
using NAudio.Wave;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using Concentus.Structs;
using Concentus.Enums;
using System.ComponentModel;
using System.Diagnostics;
using uchat.Lang; // ? ƒŒ¡¿¬À≈ÕŒ

namespace uchat.Pages
{
    // small helper to loop a WaveStream
    internal class LoopStream : WaveStream
    {
        private readonly WaveStream _sourceStream;
        public LoopStream(WaveStream sourceStream) { _sourceStream = sourceStream; }
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

    public partial class CallWindow : Window
    {
        private readonly HubService _hub;
        private bool _inCall = false;
        private int? _currentCallId = null;
        private string? _currentCallUid = null;
        private int? _callInitiatorId = null;
        private int? _currentChatId = null;

        // Native audio
        private WaveInEvent? _waveIn;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private WaveOutEvent? _waveOut;
        private bool _muted = false;
        private int? _peerUserId = null; // the other participant in 1-on-1

        // Opus encoder/decoder (kept for compatibility but not used in simplified flow)
        private OpusEncoder? _opusEncoder;
        private OpusDecoder? _opusDecoder;

        // ringtone/endcall playback via NAudio
        private WaveOutEvent? _ringWaveOut;
        private LoopStream? _ringLoopStream;
        private string? _ringTonePath;

        private WaveOutEvent? _endCallWaveOut;
        private WaveFileReader? _endCallReader;
        private string? _endCallSoundPath;

        // Jitter buffer / playout (legacy - not used in simplified flow)
        private readonly object _jitterLock = new object();
        private Dictionary<int, SortedDictionary<long, byte[]>> _jitterBuffers = new Dictionary<int, SortedDictionary<long, byte[]>>();
        private System.Threading.Timer? _playoutTimer;
        private int _jitterTargetMs = 120; // target buffering before playout
        private long _sendSequence = 0;
        private Dictionary<int, bool> _participantMuted = new Dictionary<int, bool>();

        // Timers
        private System.Threading.Timer? _noPeerTimer;
        private System.Threading.Timer? _dialCountdownTimer;

        // Dialing state
        private int _dialSecondsLeft = 30;
        private int? _dialCallId = null;

        // Simple AGC / noise gate parameters (no longer applied in simplified flow)
        private double _sendAgcGain = 1.0;
        private double _recvAgcGain = 1.0;
        private const double AGC_TARGET_RMS = 0.05;
        private const double AGC_SMOOTHING = 0.15;
        private const double AGC_MAX_GAIN = 8.0;
        private const double NOISE_GATE_THRESHOLD = 0.002;

        // Inline notification timer
        private DispatcherTimer? _notificationTimer;

        // Track whether handlers are registered on this window
        private bool _hubHandlersRegistered = false;

        public CallWindow()
        {
            InitializeComponent();
            _hub = App.HubService as HubService;
            if (_hub == null) throw new InvalidOperationException("HubService not available");

            // Subscribe to theme changes
            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            // Do NOT subscribe to hub events here. The application-level code should create the window
            // on demand and call RegisterHubHandlers once. This prevents missed events when the window
            // isn't instantiated yet and avoids double-subscription.

            // start load (fire-and-forget ok here)
            try
            {
                _ringTonePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ring.wav");
                if (!System.IO.File.Exists(_ringTonePath)) _ringTonePath = null;
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to determine ringTonePath: {ex}"); _ringTonePath = null; }

            // end-call sound
            try
            {
                _endCallSoundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "endcall.wav");
                if (!System.IO.File.Exists(_endCallSoundPath)) _endCallSoundPath = null;
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to determine endCallSoundPath: {ex}"); _endCallSoundPath = null;
}

            _ = LoadActiveCallsAsync();
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToWindow();
                System.Diagnostics.Debug.WriteLine("[CALLWINDOW] Theme changed");
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnThemeChanged failed: {ex}"); }
        }

        private void ApplyThemeToWindow()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                // Update window background
                if (this.Content is Grid mainGrid)
                {
                    var gradientBrush = new System.Windows.Media.LinearGradientBrush
                    {
                        StartPoint = new Point(1, 1),
                        EndPoint = new Point(0, 0)
                    };
                    gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(palette.BackgroundDark, 0.7));
                    gradientBrush.GradientStops.Add(new System.Windows.Media.GradientStop(palette.Primary, 0.0));
                    mainGrid.Background = gradientBrush;
                }

                System.Diagnostics.Debug.WriteLine($"[CALLWINDOW] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CALLWINDOW] ApplyThemeToWindow error: {ex.Message}");
            }
        }

        public void RegisterHubHandlers()
        {
            if (_hub == null) return;
            if (_hubHandlersRegistered) return;

            // Do not subscribe to IncomingCall here - App will forward IncomingCall events to the window explicitly
            _hub.CallAcceptedReceived += OnCallAccepted;
            _hub.CallRejectedReceived += OnCallRejected;
            _hub.CallEndedReceived += OnCallEnded;

            // audio chunk and calling indicator
            _hub.AudioChunkReceived += OnAudioChunkReceived;
            _hub.CallingReceived += OnCallingReceived;

            _hubHandlersRegistered = true;
            Debug.WriteLine("[CALLWINDOW] Hub handlers registered");
        }

        public void UnregisterHubHandlers()
        {
            if (_hub == null) return;
            if (!_hubHandlersRegistered) return;

            // IncomingCall was not registered here, so don't attempt to remove it
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
                var api = App.ApiService;
                var calls = await api.GetActiveCallsAsync();
                if (calls == null || calls.Count == 0)
                {
                    CallsListPanel.Children.Add(new TextBlock { Text = DefaultLanguage.NoActiveCall, Margin = new Thickness(6) }); // ? ÀŒ ¿À»«Œ¬¿ÕŒ
                    return;
                }

                foreach (var c in calls)
                {
                    var container = new Border { Margin = new Thickness(4), Padding = new Thickness(6), CornerRadius = new CornerRadius(6), Background = System.Windows.Media.Brushes.Transparent };
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var avatarBorder = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = System.Windows.Media.Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center };
                    var avatarImg = new Image { Width = 40, Height = 40, Stretch = System.Windows.Media.Stretch.UniformToFill };
                    avatarBorder.Child = avatarImg;
                    Grid.SetColumn(avatarBorder, 0);
                    grid.Children.Add(avatarBorder);

                    var info = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    var title = new TextBlock { Text = c.CallUid ?? $"Call {c.Id}", FontWeight = FontWeights.SemiBold };
                    var subtitle = new TextBlock { Text = $"Chat {c.ChatId}", FontSize = 12, Opacity = 0.8 };
                    info.Children.Add(title); info.Children.Add(subtitle);
                    Grid.SetColumn(info, 1);
                    grid.Children.Add(info);

                    var joinBtn = new Button { Content = "Join", Tag = new { c.Id, c.CallUid, c.ChatId, c.InitiatorId }, Margin = new Thickness(6, 0, 0, 0) };
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
                            var user = await App.ApiService.GetUserByIdAsync(c.InitiatorId);
                            if (user != null)
                            {
                                Application.Current.Dispatcher.Invoke(async () =>
                                {
                                    title.Text = user.Username ?? title.Text;
                                    if (!string.IsNullOrEmpty(user.PfpUrl))
                                    {
                                        var bmp = await App.GlobalProfilePictureCache.GetOrDownloadAsync(user.PfpUrl);
                                        if (bmp != null) avatarImg.Source = bmp;
                                    }
                                });
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] LoadActiveCalls user fetch failed: {ex}"); }
                    });
                }
            }
            catch (Exception ex)
            {
                CallsListPanel.Children.Add(new TextBlock { Text = $"{DefaultLanguage.Loading} {ex.Message}", Margin = new Thickness(6) }); // ? ÀŒ ¿À»«Œ¬¿ÕŒ
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
            CallTitle.Text = DefaultLanguage.IncomingCall; // ? ÀŒ ¿À»«Œ¬¿ÕŒ - "In call"
            CallInfoText.Text = string.Format(DefaultLanguage.Connecting, callId); // ? ÀŒ ¿À»«Œ¬¿ÕŒ

            StopRingtone();
            await _hub.AcceptCallAsync(callId);

            try
            {
                var members = await App.ApiService.GetChatMembersAsync(chatId);
                var me = App.CurrentUserId ?? 0;
                var peer = members?.FirstOrDefault(m => m.UserId != me);
                if (peer != null) _peerUserId = peer.UserId;

                // initialize participant mute map
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

                // Use simple/raw PCM path: capture 16kHz 16-bit mono and send bytes as-is; play received bytes as-is.
                _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(16000, 16, 1)) { DiscardOnBufferOverflow = true };
                _waveOut = new WaveOutEvent(); _waveOut.Init(_bufferedWaveProvider); _waveOut.Play();

                // Do not use Opus/AGC/jitter mixing - keep it simple and send/receive raw PCM
                _opusEncoder = null; _opusDecoder = null;

                // clear legacy jitter state
                lock (_jitterLock)
                {
                    _jitterBuffers.Clear();
                }

                // Dispose any old playout timer
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
                                // send raw PCM bytes to hub
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
                ShowInlineNotification($"{DefaultLanguage.CallFailed}: {ex.Message}"); // ? ÀŒ ¿À»«Œ¬¿ÕŒ
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

                _opusEncoder = null; _opusDecoder = null;
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] StopAudio failed: {ex}"); }
        }

        private void OnAudioChunkReceived(int fromUserId, byte[] chunk, int callId, long sequenceId, long timestampMs)
        {
            try
            {
                if (_currentCallId == null) return;
                if (callId != _currentCallId.Value) return;

                // Ignore audio from ourselves
                if (App.CurrentUserId.HasValue && fromUserId == App.CurrentUserId.Value) return;

                // Respect per-participant mute
                if (_participantMuted.TryGetValue(fromUserId, out var isMuted) && isMuted) return;

                // Simplified: assume incoming chunk is raw PCM 16kHz 16-bit mono and play directly
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

        // Make handler public so App can forward incoming call events to this window instance
        public void HandleIncomingCall(IncomingCallData data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Debug.WriteLine($"[CALLWINDOW] HandleIncomingCall invoked. callId={data?.CallId} callUid={data?.CallUid} metadata={data?.Metadata}");

                    // Ignore calls initiated by ourselves
                    if (data.InitiatorId == App.CurrentUserId) { Debug.WriteLine("[CALLWINDOW] Ignoring call from self"); return; }

                    // If we're already in the same call, just bring UI to front
                    if (_inCall && _currentCallId.HasValue && _currentCallId.Value == data.CallId)
                    {
                        try
                        {
                            if (!this.IsVisible) { this.Owner = Application.Current.MainWindow; this.Show(); }
                            this.WindowState = WindowState.Normal;
                            // quick topmost toggle to ensure we come to foreground
                            this.Topmost = true; this.Topmost = false;
                            this.Activate();
                            Debug.WriteLine("[CALLWINDOW] Already in call - window brought to front");
                        }
                        catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to bring window to front: {ex}"); }
                        return;
                    }

                    // If we're already in a different call, notify and ignore this incoming call
                    if (_inCall && _currentCallId.HasValue && _currentCallId.Value != data.CallId)
                    {
                        ShowInlineNotification("Incoming call received while already in a call");
                        Debug.WriteLine("[CALLWINDOW] Incoming call ignored because already in another call");
                        return;
                    }

                    // Minimal immediate UI update (do not await network calls before showing window)
                    IncomingText.Text = DefaultLanguage.IncomingCall; // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
                     IncomingFromText.Text = $"{DefaultLanguage.IncomingCall}: {data.InitiatorId}"; // ? ÀŒ ¿À≤«Œ¬¿ÕŒ

                     // Ensure incoming prompt is visually on top
                     try { Panel.SetZIndex(IncomingPrompt, 9999); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] SetZIndex failed: {ex}"); }

                     // Hide list/grid that may cover the prompt
                     try { ListViewGrid.Visibility = Visibility.Collapsed; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Hide ListViewGrid failed: {ex}"); }

                     IncomingPrompt.Visibility = Visibility.Visible;

                     // Ensure window is visible and focused
                     try
                     {
                         this.Owner = Application.Current.MainWindow;
                         if (!this.IsVisible) this.Show();
                         this.WindowState = WindowState.Normal;
                         this.Topmost = true; this.Topmost = false;
                         this.Activate();

                         Debug.WriteLine("[CALLWINDOW] Incoming prompt shown and window activated");
                     }
                     catch (Exception ex)
                     {
                         Debug.WriteLine($"[CALLWINDOW] Failed to show/activate window: {ex}");
                     }

                     // Play ringtone immediately
                     PlayRingtone();

                     // Update current call tracking
                     _currentCallId = data.CallId; _currentCallUid = data.CallUid; _callInitiatorId = data.InitiatorId; _currentChatId = data.ChatId;

                     // Add button entry in calls list
                     try
                     {
                         var panel = CallsListPanel;
                         var btn = new Button { Content = $"Incoming: {data.CallUid} from {data.InitiatorId}", Tag = new { data.CallId, data.CallUid, data.ChatId, data.InitiatorId }, Margin = new Thickness(4) };
                         btn.Click += async (s, e) => { var tag = (dynamic)btn.Tag; await JoinCallAsync((int)tag.CallId, (string)tag.CallUid, (int)tag.ChatId, (int)tag.InitiatorId); };
                         panel.Children.Insert(0, btn);
                         Debug.WriteLine("[CALLWINDOW] Added incoming call button to list");
                     }
                     catch (Exception ex)
                     {
                         Debug.WriteLine($"[CALLWINDOW] Failed to add button to calls list: {ex}");
                     }

                     // Start a no-peer timer to auto-clean if nobody answers
                     try
                     {
                         _noPeerTimer?.Dispose();
                         _noPeerTimer = new System.Threading.Timer(async _ =>
                         {
                             try
                             {
                                 if (!_inCall && _currentCallId == data.CallId)
                                 {
                                     StopRingtone();
                                     try { await _hub.EndCallAsync(data.CallId); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] NoPeer timer EndCall failed: {ex}"); }
                                     Application.Current.Dispatcher.Invoke(() =>
                                     {
                                         IncomingPrompt.Visibility = Visibility.Collapsed;
                                         var toRemove = CallsListPanel.Children.OfType<Button>().FirstOrDefault(b => ((dynamic)b.Tag).CallId == data.CallId);
                                         if (toRemove != null) CallsListPanel.Children.Remove(toRemove);
                                     });
                                 }
                             }
                             catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] NoPeer inner timer failed: {ex}"); }
                         }, null, TimeSpan.FromSeconds(30), System.Threading.Timeout.InfiniteTimeSpan);
                         Debug.WriteLine("[CALLWINDOW] No-peer timer started");
                     }
                     catch (Exception ex)
                     {
                         Debug.WriteLine($"[CALLWINDOW] Failed to start no-peer timer: {ex}");
                     }

                     // Fetch and apply caller details (username/avatar) asynchronously so UI appears immediately
                     try
                     {
                         _ = Task.Run(async () =>
                         {
                             var user = await App.ApiService.GetUserByIdAsync(data.InitiatorId);
                             if (user != null)
                             {
                                 Application.Current.Dispatcher.Invoke(async () =>
                                 {
                                     try
                                     {
                                         IncomingFromText.Text = $"From: {user.Username}";
                                     }
                                     catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Setting IncomingFromText failed: {ex}"); }

                                     try
                                     {
                                         if (!string.IsNullOrEmpty(user.PfpUrl))
                                         {
                                             var bmp = await App.GlobalProfilePictureCache.GetOrDownloadAsync(user.PfpUrl);
                                             if (bmp != null) IncomingAvatar.Source = bmp;
                                         }
                                     }
                                     catch (Exception ex)
                                     {
                                         Debug.WriteLine($"[CALLWINDOW] Failed to load avatar: {ex}");
                                     }
                                 });
                             }
                         });
                     }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] Failed to fetch user details: {ex}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HandleIncomingCall UI update error: {ex.Message}");
                }
            });
        }

        private void OnCallingReceived(int callId, string? callUid)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DialingPanel.Visibility = Visibility.Visible; 
                DialingText.Text = DefaultLanguage.Calling; // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
                _dialSecondsLeft = 30; 
                DialingCountdown.Text = _dialSecondsLeft + "s"; 
                _dialCallId = callId;
                PlayRingtone();

                _dialCountdownTimer?.Dispose();
                _dialCountdownTimer = new System.Threading.Timer(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _dialSecondsLeft--; DialingCountdown.Text = _dialSecondsLeft + "s";
                        try { System.Media.SystemSounds.Beep.Play(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Dialing beep failed: {ex.Message}"); }
                        if (_dialSecondsLeft <= 0)
                        {
                            _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; DialingPanel.Visibility = Visibility.Collapsed; if (_dialCallId.HasValue) { _ = _hub.EndCallAsync(_dialCallId.Value); _dialCallId = null; }
                        }
                    });
                }, null, 1000, 1000);
            });
        }

        private async void AcceptIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCallId == null) return;
            IncomingPrompt.Visibility = Visibility.Collapsed;

            // stop ringtone
            StopRingtone();

            // Accept and join
            await JoinCallAsync(_currentCallId.Value, _currentCallUid ?? string.Empty, _currentChatId ?? 0, _callInitiatorId ?? 0);
        }

        private async void RejectIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCallId == null) return; IncomingPrompt.Visibility = Visibility.Collapsed; StopRingtone(); try { await _hub.RejectCallAsync(_currentCallId.Value, "Rejected by user"); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] RejectIncomingButton error: {ex}"); } _currentCallId = null;
        }

        private void OnCallAccepted(int callId, int userId)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    // If we're the initiator currently dialing this call, switch to in-call state
                    if (_dialCallId.HasValue && _dialCallId.Value == callId)
                    {
                        StopRingtone();

                        _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; _dialCallId = null;
                        DialingPanel.Visibility = Visibility.Collapsed;

                        // mark as in-call and update tracking
                        _inCall = true;
                        _currentCallId = callId;
                        CallViewGrid.Visibility = Visibility.Visible;
                        ListViewGrid.Visibility = Visibility.Collapsed;
                        CallTitle.Text = "In call";
                        CallInfoText.Text = $"Call {callId} (connected)";

                        // Treat the user who accepted as peer (best-effort for 1:1)
                        try { _peerUserId = userId; _participantMuted[userId] = false; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Set peer mute state failed: {ex.Message}"); }

                        StartAudio();
                        return;
                    }

                    // If we're already in the call and someone else accepted, show a subtle notification
                    if (_currentCallId.HasValue && _currentCallId.Value == callId)
                    {
                        // don't notify for our own accept
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_currentCallId == callId)
                {
                    StopRingtone();
                    ShowInlineNotification(DefaultLanguage.CallFailed); // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
                    try { _ = _hub.EndCallAsync(callId); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnCallRejected EndCall failed: {ex}"); }
                    StopAudio();
                    PlayEndSound();
                    Close();
                    return;
                }

                if (_dialCallId == callId)
                {
                    StopRingtone();
                    ShowInlineNotification(DefaultLanguage.CallFailed); // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
                    _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; _dialCallId = null; DialingPanel.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void OnCallEnded(int callId, int userId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_currentCallId == callId)
                {
                    StopAudio();
                    PlayEndSound();
                    ShowInlineNotification(DefaultLanguage.CallEnded); // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
                    Close();
                }
            });
        }

        private async void RefreshListButton_Click(object sender, RoutedEventArgs e) { await LoadActiveCallsAsync(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private async void EndCallButton_Click(object sender, RoutedEventArgs e) { if (_currentCallId.HasValue) await _hub.EndCallAsync(_currentCallId.Value); StopAudio(); Close(); }
        private void MuteButton_Click(object sender, RoutedEventArgs e) 
        { 
            _muted = !_muted; 
            MuteButton.Content = _muted ? DefaultLanguage.Unmute : DefaultLanguage.Mute; // ? ÀŒ ¿À≤«Œ¬¿ÕŒ
        }

        private async void CancelCallButton_Click(object sender, RoutedEventArgs e)
        {
            DialingPanel.Visibility = Visibility.Collapsed; _dialCountdownTimer?.Dispose(); _dialCountdownTimer = null; if (_dialCallId.HasValue) { try { await _hub.EndCallAsync(_dialCallId.Value); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] CancelCallButton EndCall failed: {ex}"); } _dialCallId = null; }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Instead of destroying the window, hide it so it can be reused for future calls.
            e.Cancel = true;
            try { StopRingtone(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing StopRingtone failed: {ex}"); }
            try { StopAudio(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing StopAudio failed: {ex}"); }

            // Unregister hub handlers so future Show() / RegisterHubHandlers can re-subscribe
            try { UnregisterHubHandlers(); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] OnClosing UnregisterHubHandlers failed: {ex}"); }

            // Reset some UI state to a clean initial state so reopened window shows active calls list
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