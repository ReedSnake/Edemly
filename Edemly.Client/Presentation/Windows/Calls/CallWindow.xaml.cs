using Concentus.Structs;
using Edemly.Client.Application.Calls;
using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Common;
using Edemly.Contracts.Calls;
using Edemly.Contracts.Realtime;
using NAudio.Wave;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly CallSessionController _callController;
        private CallSessionState CallState => _callController.State;

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
        private readonly Dictionary<int, CallParticipantCard> _participantCards = new();
        private readonly Dictionary<int, CallParticipantDisplayInfo> _participantInfoCache = new();
        private readonly Dictionary<int, DispatcherTimer> _speakingResetTimers = new();

        private System.Threading.Timer? _dialCountdownTimer;
        private DispatcherTimer? _callDurationTimer;
        private DateTime _callDurationStartedAt = DateTime.UtcNow;
        private ImageSource? _defaultAvatarSource;
        private bool _audioStarted;

        private int _dialSecondsLeft = 30;
        private const double AGC_TARGET_RMS = 0.05;
        private const double AGC_SMOOTHING = 0.15;
        private const double AGC_MAX_GAIN = 8.0;
        private const double NOISE_GATE_THRESHOLD = 0.002;

        private DispatcherTimer? _notificationTimer;

        private bool _hubHandlersRegistered = false;

        public bool HasActiveSession => CallState.HasActiveSession;

        public CallWindow()
            : this(App.CallSessionController)
        {
        }

        public CallWindow(CallSessionController callController)
        {
            InitializeComponent();
            _callController = callController ?? throw new ArgumentNullException(nameof(callController));
            ApplyAvatarClip(IncomingAvatar, 56);
            ApplyAvatarClip(DirectAvatarImage, 128);

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
        }

        public void RegisterHubHandlers()
        {
            if (_hubHandlersRegistered) return;

            _callController.RegisterHubHandlers();
            _callController.CallAcceptedReceived += OnCallAccepted;
            _callController.CallRejectedReceived += OnCallRejected;
            _callController.CallEndedReceived += OnCallEnded;
            _callController.AudioChunkReceived += OnAudioChunkReceived;
            _callController.CallingReceived += OnCallingReceived;
            _callController.SessionChanged += OnSessionChanged;

            _hubHandlersRegistered = true;
            Debug.WriteLine("[CALLWINDOW] Hub handlers registered");
        }

        public void UnregisterHubHandlers()
        {
            if (!_hubHandlersRegistered) return;

            try { _callController.CallAcceptedReceived -= OnCallAccepted; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallAcceptedReceived: {ex}"); }
            try { _callController.CallRejectedReceived -= OnCallRejected; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallRejectedReceived: {ex}"); }
            try { _callController.CallEndedReceived -= OnCallEnded; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallEndedReceived: {ex}"); }
            try { _callController.AudioChunkReceived -= OnAudioChunkReceived; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister AudioChunkReceived: {ex}"); }
            try { _callController.CallingReceived -= OnCallingReceived; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister CallingReceived: {ex}"); }
            try { _callController.SessionChanged -= OnSessionChanged; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Failed to unregister SessionChanged: {ex}"); }

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

        private void StopDialingCountdown()
        {
            _dialCountdownTimer?.Dispose();
            _dialCountdownTimer = null;
        }

        private void HideRingingUi()
        {
            IncomingPrompt.Visibility = Visibility.Collapsed;
            DialingPanel.Visibility = Visibility.Collapsed;
            StopDialingCountdown();
        }

        private void ShowOutgoingRingingCall()
        {
            CallViewGrid.Visibility = Visibility.Collapsed;
            IncomingPrompt.Visibility = Visibility.Collapsed;
            DialingPanel.Visibility = Visibility.Visible;
            DialingText.Text = DefaultLanguage.Calling;
            _dialSecondsLeft = 30;
            DialingCountdown.Text = _dialSecondsLeft + "s";
        }

        private void ShowConnectingCall()
        {
            HideRingingUi();
            CallViewGrid.Visibility = Visibility.Visible;
            CallTitle.Text = DefaultLanguage.InCall;
            CallInfoText.Text = DefaultLanguage.Connecting;
        }

        private void ShowConnectedCall()
        {
            StopRingtone();
            HideRingingUi();
            CallViewGrid.Visibility = Visibility.Visible;
            CallTitle.Text = DefaultLanguage.InCall;
            CallInfoText.Text = DefaultLanguage.Connected;
        }

        public void ShowCurrentSession()
        {
            Dispatcher.Invoke(() =>
            {
                ShowConnectedCall();
                StartAudio();
                _ = RefreshCallVisualsAsync(CallState.Current);
            });
        }

        private void OnSessionChanged(CallSessionSnapshot snapshot)
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await RefreshCallVisualsAsync(snapshot);
            });
        }

        private async Task RefreshCallVisualsAsync(CallSessionSnapshot snapshot)
        {
            if (!snapshot.HasCall)
            {
                StopCallDurationTimer();
                StopSpeakingTimers();
                _participantCards.Clear();
                return;
            }

            ConfigureCallHeader(snapshot);
            StartCallDurationTimer(ResolveCallStartedAt(snapshot));

            if (snapshot.IsGroupCall)
            {
                DirectCallPanel.Visibility = Visibility.Collapsed;
                GroupCallPanel.Visibility = Visibility.Visible;
                await RenderGroupParticipantsAsync(snapshot);
                return;
            }

            GroupCallPanel.Visibility = Visibility.Collapsed;
            DirectCallPanel.Visibility = Visibility.Visible;
            await RenderDirectParticipantAsync(snapshot);
        }

        private void ConfigureCallHeader(CallSessionSnapshot snapshot)
        {
            CallTitle.Text = snapshot.IsGroupCall ? "Group call" : "Call";
            CallInfoText.Text = snapshot.Phase switch
            {
                CallSessionPhase.OutgoingRinging => DefaultLanguage.Calling,
                CallSessionPhase.IncomingRinging => DefaultLanguage.IncomingCall,
                CallSessionPhase.Ending => DefaultLanguage.CallEnded,
                _ => DefaultLanguage.Connected
            };

            CallMediaKindText.Text = string.Equals(snapshot.MediaKind, CallMediaKinds.Video, StringComparison.OrdinalIgnoreCase)
                ? "Video"
                : "Audio";
        }

        private async Task RenderDirectParticipantAsync(CallSessionSnapshot snapshot)
        {
            var participant = ResolveDirectParticipant(snapshot);
            if (participant == null)
            {
                DirectParticipantNameText.Text = "Participant";
                DirectParticipantStatusText.Text = DefaultLanguage.Connecting;
                DirectMicStatusText.Text = "Mic on";
                DirectAvatarImage.Source = GetDefaultAvatarSource();
                StopSpeakingTimers();
                _participantCards.Clear();
                return;
            }

            var card = new CallParticipantCard(
                participant.UserId,
                DirectAvatarBorder,
                DirectAvatarImage,
                DirectParticipantNameText,
                DirectParticipantStatusText,
                DirectMicStatusText);

            StopSpeakingTimers();
            _participantCards.Clear();
            _participantCards[participant.UserId] = card;

            ApplyParticipantState(card, participant);
            await ApplyParticipantIdentityAsync(card, participant.UserId);
        }

        private async Task RenderGroupParticipantsAsync(CallSessionSnapshot snapshot)
        {
            var participants = ResolveGroupParticipants(snapshot);
            GroupParticipantsPanel.Children.Clear();
            StopSpeakingTimers();
            _participantCards.Clear();

            if (participants.Count == 0)
            {
                var currentUserId = App.CurrentUserId ?? snapshot.InitiatorId;
                if (currentUserId.HasValue)
                {
                    participants.Add(new CallParticipantDto
                    {
                        UserId = currentUserId.Value,
                        Status = CallParticipantStatuses.Joined,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            foreach (var participant in participants)
            {
                var card = CreateGroupParticipantCard(participant.UserId);
                _participantCards[participant.UserId] = card;
                if (card.Root != null)
                {
                    GroupParticipantsPanel.Children.Add(card.Root);
                }
                ApplyParticipantState(card, participant);
            }

            foreach (var participant in participants)
            {
                if (_participantCards.TryGetValue(participant.UserId, out var card))
                {
                    await ApplyParticipantIdentityAsync(card, participant.UserId);
                }
            }
        }

        private CallParticipantDto? ResolveDirectParticipant(CallSessionSnapshot snapshot)
        {
            var currentUserId = App.CurrentUserId;
            var peerUserId = snapshot.PeerUserId
                ?? snapshot.Participants.FirstOrDefault(participant =>
                    !currentUserId.HasValue || participant.UserId != currentUserId.Value)?.UserId
                ?? (snapshot.InitiatorId.HasValue && (!currentUserId.HasValue || snapshot.InitiatorId.Value != currentUserId.Value)
                    ? snapshot.InitiatorId
                    : null);

            if (!peerUserId.HasValue)
            {
                return null;
            }

            return snapshot.Participants.FirstOrDefault(participant => participant.UserId == peerUserId.Value)
                ?? new CallParticipantDto
                {
                    UserId = peerUserId.Value,
                    Status = snapshot.Phase == CallSessionPhase.InCall
                        ? CallParticipantStatuses.Joined
                        : CallParticipantStatuses.Ringing
                };
        }

        private List<CallParticipantDto> ResolveGroupParticipants(CallSessionSnapshot snapshot)
        {
            return snapshot.Participants
                .Where(participant => participant.UserId > 0)
                .OrderByDescending(participant => string.Equals(participant.Status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase))
                .ThenBy(participant => participant.JoinedAt ?? participant.InvitedAt ?? DateTime.MaxValue)
                .ToList();
        }

        private CallParticipantCard CreateGroupParticipantCard(int userId)
        {
            var root = new Border
            {
                Width = 132,
                Height = 154,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10),
                BorderThickness = new Thickness(1)
            };
            root.SetResourceReference(Border.BackgroundProperty, "ThemeSurfaceBrush");
            root.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderLightBrush");

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var avatarBorder = new Border
            {
                Width = 70,
                Height = 70,
                CornerRadius = new CornerRadius(35),
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent
            };
            avatarBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderLightBrush");

            var avatarImage = new Image
            {
                Width = 70,
                Height = 70,
                Clip = new EllipseGeometry(new Point(35, 35), 35, 35),
                Stretch = Stretch.UniformToFill,
                Source = GetDefaultAvatarSource()
            };
            avatarBorder.Child = avatarImage;

            var nameText = new TextBlock
            {
                Text = $"User {userId}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 104,
                Margin = new Thickness(0, 8, 0, 0)
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");

            var statusText = new TextBlock
            {
                Text = DefaultLanguage.Connected,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 104,
                Margin = new Thickness(0, 2, 0, 0)
            };
            statusText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");

            var micText = new TextBlock
            {
                Text = "Mic on",
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            micText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");

            stack.Children.Add(avatarBorder);
            stack.Children.Add(nameText);
            stack.Children.Add(statusText);
            stack.Children.Add(micText);
            root.Child = stack;

            return new CallParticipantCard(userId, avatarBorder, avatarImage, nameText, statusText, micText, root);
        }

        private void ApplyParticipantState(CallParticipantCard card, CallParticipantDto participant)
        {
            var isCurrentUser = App.CurrentUserId == participant.UserId;
            if (isCurrentUser)
            {
                _muted = participant.IsMuted;
                MuteButton.Content = _muted ? DefaultLanguage.Unmute : DefaultLanguage.Mute;
            }

            var isMuted = isCurrentUser
                ? _muted
                : participant.IsMuted;

            _participantMuted[participant.UserId] = isMuted;
            card.StatusText.Text = ResolveParticipantStatusText(participant.Status);
            card.MicText.Text = isMuted
                ? "Muted"
                : "Mic on";
        }

        private async Task ApplyParticipantIdentityAsync(CallParticipantCard card, int userId)
        {
            var info = await GetParticipantDisplayInfoAsync(userId);
            if (!_participantCards.TryGetValue(userId, out var currentCard) || !ReferenceEquals(currentCard, card))
            {
                return;
            }

            card.NameText.Text = info.DisplayName;
            card.AvatarImage.Source = info.Avatar ?? GetDefaultAvatarSource();
        }

        private async Task<CallParticipantDisplayInfo> GetParticipantDisplayInfoAsync(int userId)
        {
            if (_participantInfoCache.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            var displayName = App.CurrentUserId == userId && !string.IsNullOrWhiteSpace(App.CurrentUserName)
                ? App.CurrentUserName!
                : $"User {userId}";
            var photoUrl = App.CurrentUserId == userId ? App.CurrentUserPhotoUrl : null;

            try
            {
                var user = await App.ApiClients.Users.GetUserByIdAsync(userId);
                if (user != null)
                {
                    displayName = ResolveDisplayName(user.FirstName, user.LastName, user.Username, user.Id);
                    photoUrl = user.PfpUrl;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALLWINDOW] Failed to load participant {userId}: {ex.Message}");
            }

            ImageSource? avatar = null;
            if (!string.IsNullOrWhiteSpace(photoUrl))
            {
                try
                {
                    avatar = await App.GlobalProfilePictureCache.GetOrDownloadAsync(photoUrl);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CALLWINDOW] Failed to load participant avatar {userId}: {ex.Message}");
                }
            }

            var info = new CallParticipantDisplayInfo(displayName, avatar);
            _participantInfoCache[userId] = info;
            return info;
        }

        private static string ResolveDisplayName(string? firstName, string? lastName, string? username, int userId)
        {
            var fullName = $"{firstName} {lastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            return !string.IsNullOrWhiteSpace(username)
                ? username!
                : $"User {userId}";
        }

        private static string ResolveParticipantStatusText(string? status)
        {
            if (string.Equals(status, CallParticipantStatuses.Joined, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLanguage.Connected;
            }

            if (string.Equals(status, CallParticipantStatuses.Ringing, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, CallParticipantStatuses.Invited, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLanguage.Connecting;
            }

            if (string.Equals(status, CallParticipantStatuses.Missed, StringComparison.OrdinalIgnoreCase))
            {
                return "Missed";
            }

            if (string.Equals(status, CallParticipantStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                return "Rejected";
            }

            return "Left";
        }

        private DateTime ResolveCallStartedAt(CallSessionSnapshot snapshot)
        {
            return snapshot.Participants
                .Select(participant => participant.JoinedAt ?? participant.InvitedAt)
                .Where(time => time.HasValue)
                .Select(time => NormalizeServerUtc(time!.Value))
                .DefaultIfEmpty(DateTime.UtcNow)
                .Min();
        }

        private void StartCallDurationTimer(DateTime startedAt)
        {
            _callDurationStartedAt = startedAt;
            UpdateCallDurationText();

            if (_callDurationTimer != null)
            {
                return;
            }

            _callDurationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _callDurationTimer.Tick += (_, _) => UpdateCallDurationText();
            _callDurationTimer.Start();
        }

        private void StopCallDurationTimer()
        {
            _callDurationTimer?.Stop();
            _callDurationTimer = null;
            CallDurationText.Text = "00:00";
        }

        private void UpdateCallDurationText()
        {
            var duration = DateTime.UtcNow - NormalizeServerUtc(_callDurationStartedAt);
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            CallDurationText.Text = duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"mm\:ss");
        }

        private static DateTime NormalizeServerUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static void ApplyAvatarClip(Image image, double size)
        {
            image.Width = size;
            image.Height = size;
            image.Stretch = Stretch.UniformToFill;
            image.Clip = new EllipseGeometry(new Point(size / 2, size / 2), size / 2, size / 2);
            image.SnapsToDevicePixels = true;
        }

        private ImageSource? GetDefaultAvatarSource()
        {
            if (_defaultAvatarSource != null)
            {
                return _defaultAvatarSource;
            }

            try
            {
                var image = new BitmapImage(new Uri(Edemly.Client.Models.Contact.DefaultAvatarPath, UriKind.RelativeOrAbsolute));
                image.Freeze();
                _defaultAvatarSource = image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALLWINDOW] Failed to load default avatar: {ex.Message}");
            }

            return _defaultAvatarSource;
        }

        private void MarkParticipantSpeaking(int userId)
        {
            if (!_participantCards.TryGetValue(userId, out var card))
            {
                return;
            }

            card.AvatarBorder.BorderBrush = Brushes.White;
            card.AvatarBorder.BorderThickness = new Thickness(4);

            if (_speakingResetTimers.TryGetValue(userId, out var existingTimer))
            {
                existingTimer.Stop();
            }

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(850)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ResetParticipantSpeaking(card);
                _speakingResetTimers.Remove(userId);
            };

            _speakingResetTimers[userId] = timer;
            timer.Start();
        }

        private void StopSpeakingTimers()
        {
            foreach (var timer in _speakingResetTimers.Values)
            {
                timer.Stop();
            }

            _speakingResetTimers.Clear();
        }

        private static void ResetParticipantSpeaking(CallParticipantCard card)
        {
            card.AvatarBorder.BorderThickness = new Thickness(2);
            card.AvatarBorder.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderLightBrush");
        }

        private static bool HasVoiceActivity(byte[] buffer, int length)
        {
            if (buffer.Length < 2 || length < 2)
            {
                return false;
            }

            var sampleCount = Math.Min(buffer.Length, length) / 2;
            if (sampleCount == 0)
            {
                return false;
            }

            double sumSquares = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = BitConverter.ToInt16(buffer, i * 2) / 32768.0;
                sumSquares += sample * sample;
            }

            var rms = Math.Sqrt(sumSquares / sampleCount);
            return rms > NOISE_GATE_THRESHOLD * 3;
        }

        private async Task LeavePreviousCallIfAnyAsync()
        {
            try
            {
                var current = CallState.Current;
                if (current.Phase == CallSessionPhase.InCall && current.CallId.HasValue)
                {
                    try { await _callController.EndCurrentAsync(DefaultLanguage.CallEnded); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] LeavePreviousCall EndCall failed: {ex}"); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] LeavePreviousCallIfAnyAsync failed: {ex}"); }
            finally
            {
                StopAudio();
                CallState.Clear();
            }
        }

        private async Task JoinCallAsync(int callId, string callUid, int chatId, int initiatorId)
        {
            var current = CallState.Current;
            if (current.Phase == CallSessionPhase.InCall && current.CallId != callId) await LeavePreviousCallIfAnyAsync();
            if (CallState.Current.Phase == CallSessionPhase.InCall) return;

            ShowConnectingCall();

            StopRingtone();
            var accepted = await _callController.AcceptCurrentAsync();
            if (accepted == null)
            {
                return;
            }

            try
            {
                var members = await App.ApiClients.ChatMembers.GetChatMembersAsync(chatId);
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

            ShowConnectedCall();
            StartAudio();
        }

        private void StartAudio()
        {
            try
            {
                if (_audioStarted) return;

                var callId = CallState.Current.CallId;
                if (callId == null) return;

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
                        if (App.CurrentUserId.HasValue && HasVoiceActivity(bytes, a.BytesRecorded))
                        {
                            Dispatcher.Invoke(() => MarkParticipantSpeaking(App.CurrentUserId.Value));
                        }

                        var sequence = Interlocked.Increment(ref _sendSequence);
                        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _callController.SendAudioChunkAsync(null, bytes, callId.Value, sequence, timestampMs);
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
                _audioStarted = true;
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
                _bufferedWaveProvider = null; _peerUserId = null;
                _audioStarted = false;

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
                var currentCallId = CallState.Current.CallId;
                if (currentCallId == null) return;
                if (callId != currentCallId.Value) return;

                if (App.CurrentUserId.HasValue && fromUserId == App.CurrentUserId.Value) return;

                if (_participantMuted.TryGetValue(fromUserId, out var isMuted) && isMuted) return;

                try
                {
                    if (chunk != null && chunk.Length > 0 && _bufferedWaveProvider != null)
                    {
                        _bufferedWaveProvider.AddSamples(chunk, 0, chunk.Length);
                        MarkParticipantSpeaking(fromUserId);
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

                    if (App.CurrentUserId.HasValue && incomingCall.InitiatorId == App.CurrentUserId.Value)
                    {
                        Debug.WriteLine("[CALLWINDOW] Incoming call ignored because current user is the initiator");
                        return;
                    }

                    var current = CallState.Current;
                    if (current.CallId.HasValue && current.CallId.Value == incomingCall.CallId)
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

                    if (current.Phase != CallSessionPhase.Idle && current.CallId.HasValue && current.CallId.Value != incomingCall.CallId)
                    {
                        ShowInlineNotification("Incoming call received while already in a call");
                        Debug.WriteLine("[CALLWINDOW] Incoming call ignored because already in another call");
                        return;
                    }

                    IncomingText.Text = DefaultLanguage.IncomingCall;
                    IncomingFromText.Text = $"{DefaultLanguage.IncomingCall}: {incomingCall.InitiatorId}";
                    CallViewGrid.Visibility = Visibility.Collapsed;
                    DialingPanel.Visibility = Visibility.Collapsed;

                    try
                    {
                        Panel.SetZIndex(IncomingPrompt, 9999);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CALLWINDOW] SetZIndex failed: {ex}");
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

                    _callController.BeginIncoming(incomingCall);

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
        private void OnCallingReceived(CallSessionSnapshot snapshot)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (snapshot.IsGroupCall)
                {
                    StopRingtone();
                    StopDialingCountdown();
                    ShowConnectedCall();
                    StartAudio();
                    return;
                }

                ShowOutgoingRingingCall();
                PlayRingtone();

                StopDialingCountdown();
                _dialCountdownTimer = new System.Threading.Timer(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _dialSecondsLeft--;
                        DialingCountdown.Text = _dialSecondsLeft + "s";

                        if (_dialSecondsLeft <= 0)
                        {
                            StopDialingCountdown();
                            DialingCountdown.Text = "0s";
                        }
                    });
                }, null, 1000, 1000);
            });
        }

        private async void AcceptIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            var current = CallState.Current;
            if (current.CallId == null) return;
            IncomingPrompt.Visibility = Visibility.Collapsed;

            StopRingtone();

            await JoinCallAsync(current.CallId.Value, current.CallUid ?? string.Empty, current.ChatId ?? 0, current.InitiatorId ?? 0);
        }

        private async void RejectIncomingButton_Click(object sender, RoutedEventArgs e)
        {
            var current = CallState.Current;
            if (current.CallId == null) return; IncomingPrompt.Visibility = Visibility.Collapsed; StopRingtone(); try { await _callController.RejectCurrentAsync("Rejected by user"); } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] RejectIncomingButton error: {ex}"); }
        }

        private void OnCallAccepted(int callId, int userId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var current = CallState.Current;
                    if (current.CallId.HasValue && current.CallId.Value == callId)
                    {
                        var wasAwaitingConnection =
                            DialingPanel.Visibility == Visibility.Visible
                            || IncomingPrompt.Visibility == Visibility.Visible
                            || !_audioStarted;
                        var isCurrentUser = App.CurrentUserId.HasValue && App.CurrentUserId.Value == userId;

                        ShowConnectedCall();

                        try { _peerUserId = userId; _participantMuted[userId] = false; } catch (Exception ex) { Debug.WriteLine($"[CALLWINDOW] Set peer mute state failed: {ex.Message}"); }

                        StartAudio();

                        if (!isCurrentUser && !wasAwaitingConnection)
                        {
                            ShowInlineNotification(DefaultLanguage.ParticipantJoined, 3);
                        }
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
                StopRingtone();
                ShowInlineNotification(DefaultLanguage.CallFailed); // ? ����˲������
                HideRingingUi();
                StopAudio();
                PlayEndSound();
                Close();
            });
        }

        private void OnCallEnded(int callId, int userId)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StopRingtone();
                HideRingingUi();
                StopAudio();
                PlayEndSound();
                ShowInlineNotification(DefaultLanguage.CallEnded); // ? ����˲������
                Close();
            });
        }

        private async Task CloseCurrentSessionAsync()
        {
            try
            {
                await _callController.CloseCurrentAsync("Window closed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CALLWINDOW] CloseCurrentSession failed: {ex}");
            }
        }

        private async void EndCallButton_Click(object sender, RoutedEventArgs e)
        {
            var currentCallId = CallState.Current.CallId;
            if (currentCallId.HasValue) await _callController.EndCurrentAsync(DefaultLanguage.CallEnded);
            StopAudio();
            Close();
        }

        private async void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            var nextMuted = !_muted;
            _muted = nextMuted;
            MuteButton.Content = _muted ? DefaultLanguage.Unmute : DefaultLanguage.Mute;

            try
            {
                await _callController.SetMutedCurrentAsync(nextMuted);
            }
            catch (Exception ex)
            {
                _muted = !nextMuted;
                MuteButton.Content = _muted ? DefaultLanguage.Unmute : DefaultLanguage.Mute;
                _ = RefreshCallVisualsAsync(CallState.Current);
                ShowInlineNotification($"{DefaultLanguage.CallFailed}: {ex.Message}");
            }
        }

        private async void CancelCallButton_Click(object sender, RoutedEventArgs e)
        {
            DialingPanel.Visibility = Visibility.Collapsed;

            StopDialingCountdown();

            StopRingtone();

            var currentCallId = CallState.Current.CallId;
            if (currentCallId.HasValue)
            {
                try
                {
                    await _callController.EndCurrentAsync("Canceled");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CALLWINDOW] CancelCallButton EndCall failed: {ex}");
                }
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
                NotificationBorder.Visibility = Visibility.Collapsed;
                StopDialingCountdown();
                StopCallDurationTimer();
                StopSpeakingTimers();
                _dialSecondsLeft = 30;
                if (CallState.HasActiveSession)
                {
                    _ = CloseCurrentSessionAsync();
                }
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
            StopDialingCountdown();
            StopCallDurationTimer();
            StopSpeakingTimers();
        }

        private sealed class CallParticipantCard
        {
            public CallParticipantCard(
                int userId,
                Border avatarBorder,
                Image avatarImage,
                TextBlock nameText,
                TextBlock statusText,
                TextBlock micText,
                Border? root = null)
            {
                UserId = userId;
                AvatarBorder = avatarBorder;
                AvatarImage = avatarImage;
                NameText = nameText;
                StatusText = statusText;
                MicText = micText;
                Root = root;
            }

            public int UserId { get; }

            public Border AvatarBorder { get; }

            public Image AvatarImage { get; }

            public TextBlock NameText { get; }

            public TextBlock StatusText { get; }

            public TextBlock MicText { get; }

            public Border? Root { get; }
        }

        private sealed record CallParticipantDisplayInfo(
            string DisplayName,
            ImageSource? Avatar);
    }
}
