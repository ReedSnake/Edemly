#nullable disable

using Edemly.Contracts.Messages;
using NAudio.Wave;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public static partial class VoiceMessageHelper
    {
        private static IWavePlayer _waveOut;
        private static AudioFileReader _audioFile;
        private static DispatcherTimer _playbackTimer;
        private static Border _currentPlayingBorder;
        private static Button _currentPlayButton;
        private static Slider _currentSlider;
        private static TextBlock _currentTimeText;
        private static int _currentMessageId = -1;
        private static bool _isUserDragging = false;

        public static void StopPlaybackForUiRefresh()
        {
            StopAudio();
        }

        private static async Task HandlePlayPauseAsync(
            MessageDto message,
            Button playButton,
            Slider slider,
            TextBlock timeText,
            Border messageBorder,
            double? startAtSeconds = null)
        {
            try
            {
                if (_currentMessageId != -1 && _currentMessageId != message.Id)
                {
                    StopAudio();
                }

                if (_currentMessageId == message.Id && _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    _waveOut.Pause();
                    playButton.Content = "\u25B6";
                    playButton.Tag = "paused";
                    return;
                }

                if (_currentMessageId == message.Id && _waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
                {
                    _waveOut.Play();
                    playButton.Content = "\u23F8";
                    playButton.Tag = "playing";
                    return;
                }

                var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, "voice.wav");
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] File not found");
                    return;
                }

                _audioFile = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioFile);

                _currentPlayingBorder = messageBorder;
                _currentPlayButton = playButton;
                _currentSlider = slider;
                _currentTimeText = timeText;
                _currentMessageId = message.Id;

                var total = _audioFile.TotalTime.TotalSeconds;
                slider.Minimum = 0;
                slider.Maximum = Math.Max(1, total);

                if (startAtSeconds.HasValue)
                {
                    var pos = Math.Min(startAtSeconds.Value, _audioFile.TotalTime.TotalSeconds);
                    _audioFile.CurrentTime = TimeSpan.FromSeconds(pos);
                }

                if (_playbackTimer == null)
                {
                    _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    _playbackTimer.Tick += PlaybackTimer_Tick;
                }

                _playbackTimer.Start();

                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();

                playButton.Content = "\u23F8";
                playButton.Tag = "playing";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VOICE] Error playing: {ex.Message}");
                playButton.Content = "\u25B6";
                playButton.Tag = "play";
            }
        }

        private static void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_audioFile == null || _currentSlider == null || _currentTimeText == null)
                {
                    return;
                }

                if (_isUserDragging)
                {
                    return;
                }

                var current = _audioFile.CurrentTime;
                var total = _audioFile.TotalTime;

                _currentSlider.Value = Math.Min(_currentSlider.Maximum, current.TotalSeconds);
                _currentTimeText.Text = $"{FormatTime(current)} / {FormatTime(total)}";

                if (total.TotalSeconds > 0 && _currentSlider.ActualWidth > 0 && _currentSlider.Template != null)
                {
                    try
                    {
                        var ratio = current.TotalSeconds / total.TotalSeconds;
                        var progressWidth = ratio * _currentSlider.ActualWidth;

                        var progressTrack = _currentSlider.Template.FindName("ProgressTrack", _currentSlider) as Border;
                        if (progressTrack != null)
                        {
                            progressTrack.Width = Math.Max(0, Math.Min(progressWidth, _currentSlider.ActualWidth));
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void SeekAudio(double seconds)
        {
            try
            {
                if (_audioFile == null)
                {
                    return;
                }

                var ts = TimeSpan.FromSeconds(Math.Min(seconds, _audioFile.TotalTime.TotalSeconds));
                _audioFile.CurrentTime = ts;
            }
            catch
            {
            }
        }

        private static void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_currentPlayButton != null)
                    {
                        _currentPlayButton.Content = "\u25B6";
                        _currentPlayButton.Tag = "play";
                    }

                    if (_currentSlider != null)
                    {
                        _currentSlider.Value = 0;

                        try
                        {
                            if (_currentSlider.Template != null)
                            {
                                var progressTrack = _currentSlider.Template.FindName("ProgressTrack", _currentSlider) as Border;
                                if (progressTrack != null)
                                {
                                    progressTrack.Width = 0;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    if (_currentTimeText != null && _audioFile != null)
                    {
                        _currentTimeText.Text = $"00:00 / {FormatTime(_audioFile.TotalTime)}";
                    }
                }
                catch
                {
                }
                finally
                {
                    CleanupPlayback();
                }
            });
        }

        private static void CleanupPlayback()
        {
            try
            {
                _playbackTimer?.Stop();

                if (_waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioFile != null)
                {
                    _audioFile.Dispose();
                    _audioFile = null;
                }

                try
                {
                    if (_currentSlider != null && _currentSlider.Template != null)
                    {
                        var progressTrack = _currentSlider.Template.FindName("ProgressTrack", _currentSlider) as Border;
                        if (progressTrack != null)
                        {
                            progressTrack.Width = 0;
                        }
                    }
                }
                catch
                {
                }

                _currentMessageId = -1;
                _currentPlayingBorder = null;
                _currentPlayButton = null;
                _currentSlider = null;
                _currentTimeText = null;
            }
            catch
            {
            }
        }

        private static void StopAudio()
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                }

                CleanupPlayback();
            }
            catch
            {
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            return string.Format("{0:D2}:{1:D2}", (int)time.TotalMinutes, time.Seconds);
        }

        private static void PrefetchDuration(MessageDto message, Slider slider, TextBlock timeText)
        {
            Task.Run(async () =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, "voice.wav");
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        return;
                    }

                    using var audioFileReader = new AudioFileReader(filePath);
                    var total = audioFileReader.TotalTime;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            if (slider != null)
                            {
                                slider.Minimum = 0;
                                slider.Maximum = Math.Max(1, total.TotalSeconds);
                            }

                            if (timeText != null)
                            {
                                timeText.Text = $"00:00 / {FormatTime(total)}";
                            }

                            try
                            {
                                if (slider != null && slider.Template != null)
                                {
                                    var progressTrack = slider.Template.FindName("ProgressTrack", slider) as Border;
                                    if (progressTrack != null)
                                    {
                                        progressTrack.Width = 0;
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                        catch
                        {
                        }
                    });
                }
                catch
                {
                }
            });
        }
    }
}
