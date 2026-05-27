#nullable disable
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using uchat.DTOs;
using NAudio.Wave;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Markup;
using uchat.Lang;
using uchat.Services;

namespace uchat.Helpers
{
    /// <summary>
    /// Допоміжний клас для рендерингу голосових повідомлень
    /// </summary>
    public static class VoiceMessageHelper
    {
        // Shared playback resources so only one audio can be played at a time
        private static IWavePlayer _waveOut;
        private static AudioFileReader _audioFile;
        private static DispatcherTimer _playbackTimer;
        private static Border _currentPlayingBorder;
        private static Button _currentPlayButton;
        private static Slider _currentSlider;
        private static TextBlock _currentTimeText;
        private static int _currentMessageId = -1;
        private static bool _isUserDragging = false;

        // Cached parsed template for sliders (parsed once)
        private static ControlTemplate _cachedSliderTemplate;
        private static readonly object _templateLock = new object();

        public static void AddMyVoiceMessage(MessageDto message, StackPanel messagesPanel, int currentUserId, bool isHistorical)
        {
            var border = BuildVoiceMessageBorder(message, true, messagesPanel, currentUserId, isHistorical, senderName: null, isGroupChat: false);
            messagesPanel.Children.Add(border);
            if (!isHistorical) AnimateFadeIn(border);
        }

        public static void AddFriendVoiceMessage(MessageDto message, StackPanel messagesPanel, int currentUserId, bool isHistorical, string senderName, bool isGroupChat)
        {
            var border = BuildVoiceMessageBorder(message, false, messagesPanel, currentUserId, isHistorical, senderName, isGroupChat);
            messagesPanel.Children.Add(border);
            if (!isHistorical) AnimateFadeIn(border);
        }

        private static Border BuildVoiceMessageBorder(MessageDto message, bool isMine, StackPanel messagesPanel, int currentUserId, bool isHistorical, string senderName, bool isGroupChat)
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            
            // Colors depending on side and theme
            var bg = isMine ? palette.BorderLight : palette.Primary;
            var playBtnBg = isMine ? palette.Primary : palette.BorderLight;
            var playBtnFg = isMine ? Brushes.White : new SolidColorBrush(palette.Primary);
            var progressColor = isMine ? new SolidColorBrush(palette.Primary) : Brushes.White;
            var textColor = isMine ? new SolidColorBrush(palette.TextPrimary) : Brushes.White;

            Border messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(bg),
                CornerRadius = isMine ? new CornerRadius(15, 15, 0, 15) : new CornerRadius(15, 15, 15, 0),
                Margin = isMine ? new Thickness(150, 8, 15, 8) : new Thickness(15, 8, 150, 8),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 300,
                Opacity = isHistorical ? 0.8 : 1
            };

            StackPanel mainPanel = new StackPanel();

            if (isGroupChat && !isMine && !string.IsNullOrEmpty(senderName))
            {
                TextBlock senderNameText = new TextBlock
                {
                    Text = senderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 220)),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                mainPanel.Children.Add(senderNameText);
            }

            StackPanel stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Play/pause button
            Button playButton = CreateCircularButton(playBtnBg, playBtnFg);

            // Slider and time
            Slider positionSlider = CreateCustomSlider();
            ApplySliderColors(positionSlider, progressColor);

            TextBlock timeText = new TextBlock
            {
                Text = "00:00 / 00:00",
                FontSize = 12,
                Foreground = textColor,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Wire up play/pause
            playButton.Click += async (s, e) => await HandlePlayPauseAsync(message, playButton, positionSlider, timeText, messageBorder);

            // Slider drag handling
            positionSlider.PreviewMouseDown += (s, e) => { _isUserDragging = true; };
            positionSlider.PreviewMouseUp += async (s, e) =>
            {
                _isUserDragging = false;
                if (_currentMessageId == message.Id && _audioFile != null)
                {
                    SeekAudio(positionSlider.Value);
                }
                else if (_currentMessageId != message.Id)
                {
                    await HandlePlayPauseAsync(message, playButton, positionSlider, timeText, messageBorder, startAtSeconds: positionSlider.Value);
                }
            };

            // Info panel
            StackPanel infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock voiceLabel = new TextBlock
            {
                Text = DefaultLanguage.VoiceMessage,
                FontSize = 13,
                Foreground = textColor,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            TextBlock timeSent = new TextBlock
            {
                Text = isHistorical ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm") : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = textColor,
                Opacity = 0.7
            };
            infoPanel.Children.Add(voiceLabel);
            infoPanel.Children.Add(timeSent);

            StackPanel controlsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            controlsPanel.Children.Add(playButton);
            controlsPanel.Children.Add(positionSlider);
            controlsPanel.Children.Add(timeText);

            stackPanel.Children.Add(controlsPanel);
            stackPanel.Children.Add(infoPanel);
            mainPanel.Children.Add(stackPanel);
            messageBorder.Child = mainPanel;

            AddVoiceMessageContextMenu(messageBorder, message, currentUserId);

            PrefetchDuration(message, positionSlider, timeText);

            return messageBorder;
        }

        private static void AnimateFadeIn(UIElement element)
        {
            DoubleAnimation fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.3) };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private static Button CreateCircularButton(Color bgColor, Brush foreground)
        {
            Button playButton = new Button
            {
                Content = "\u25B6",
                Width = 40,
                Height = 40,
                FontSize = 16,
                Background = new SolidColorBrush(bgColor),
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                Tag = "play",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var btnTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(20));
            var backgroundBinding = new System.Windows.Data.Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) };
            var borderBrushBinding = new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) };
            borderFactory.SetBinding(Border.BackgroundProperty, backgroundBinding);
            borderFactory.SetBinding(Border.BorderBrushProperty, borderBrushBinding);
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);
            btnTemplate.VisualTree = borderFactory;
            playButton.Template = btnTemplate;

            return playButton;
        }

        private static Slider CreateCustomSlider()
        {
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Height = 24,
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                FocusVisualStyle = null
            };

// parse template once
if (_cachedSliderTemplate == null)
            {
                lock (_templateLock)
                {
                    if (_cachedSliderTemplate == null)
                    {
                        string sliderTemplateXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
               xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
               TargetType='Slider'> <Grid Height='24' VerticalAlignment='Center'> <Border x:Name='BaseTrack' Height='4' VerticalAlignment='Center' CornerRadius='2' 
         Background='#D0D0D0' BorderThickness='0' Focusable='False'/> <Grid> <Border x:Name='ProgressTrack' Height='4' VerticalAlignment='Center' CornerRadius='2' 
           Background='#808080' HorizontalAlignment='Left' Width='0' BorderThickness='0' Focusable='False'/> <Track x:Name='PART_Track' VerticalAlignment='Center' Focusable='False'>
<Track.DecreaseRepeatButton> <RepeatButton Command='Slider.DecreaseLarge' Background='Transparent'
                     BorderThickness='0' BorderBrush='Transparent' IsTabStop='False'/>
</Track.DecreaseRepeatButton>
<Track.IncreaseRepeatButton> <RepeatButton Command='Slider.IncreaseLarge' Background='Transparent'
                     BorderThickness='0' BorderBrush='Transparent' IsTabStop='False'/>
</Track.IncreaseRepeatButton>
<Track.Thumb> <Thumb Width='14' Height='14' Focusable='False'>
<Thumb.Template> <ControlTemplate TargetType='Thumb'> <Ellipse Width='14' Height='14' Fill='{TemplateBinding Background}' StrokeThickness='0'/> </ControlTemplate>
</Thumb.Template> </Thumb>
</Track.Thumb> </Track> </Grid> </Grid> </ControlTemplate>";

            _cachedSliderTemplate = (ControlTemplate)XamlReader.Parse(sliderTemplateXaml);
                    }
                }
            }

            if (_cachedSliderTemplate != null)
            {
                slider.Template = _cachedSliderTemplate;
            }

            return slider;

}


        private static void ApplySliderColors(Slider slider, Brush progressBrush)
        {
            if (slider == null) return;
            // Foreground used for thumb background in some templates
            slider.Foreground = progressBrush;

            // Try to set ProgressTrack background
            try
            {
                if (slider.Template != null)
                {
                    var progressTrack = slider.Template.FindName("ProgressTrack", slider) as Border;
                    if (progressTrack != null)
                    {
                        progressTrack.Background = progressBrush;
                    }
                }
            }
            catch { }
        }

        private static async Task HandlePlayPauseAsync(MessageDto message, Button playButton, Slider slider, TextBlock timeText, Border messageBorder, double? startAtSeconds = null)
        {
            try
            {
                // If clicking play for a different message, stop current
                if (_currentMessageId != -1 && _currentMessageId != message.Id)
                {
                    StopAudio();
                }

                // If this is the currently playing message
                if (_currentMessageId == message.Id && _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    // Pause
                    _waveOut.Pause();
                    playButton.Content = "\u25B6"; // play symbol
                    playButton.Tag = "paused";
                    return;
                }

                // If paused for this message -> resume
                if (_currentMessageId == message.Id && _waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
                {
                    _waveOut.Play();
                    playButton.Content = "\u23F8"; // pause icon
                    playButton.Tag = "playing";
                    return;
                }

                // Start new playback
                var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, "voice.wav");
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE] File not found");
                    return;
                }

                // Initialize audio playback
                _audioFile = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioFile);

                // Setup UI references
                _currentPlayingBorder = messageBorder;
                _currentPlayButton = playButton;
                _currentSlider = slider;
                _currentTimeText = timeText;
                _currentMessageId = message.Id;

                // Set slider maximum to duration
                var total = _audioFile.TotalTime.TotalSeconds;
                slider.Minimum = 0;
                slider.Maximum = Math.Max(1, total);

                // If user requested start position
                if (startAtSeconds.HasValue)
                {
                    var pos = Math.Min(startAtSeconds.Value, _audioFile.TotalTime.TotalSeconds);
                    _audioFile.CurrentTime = TimeSpan.FromSeconds(pos);
                }

                // Start timer for UI updates
                if (_playbackTimer == null)
                {
                    _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    _playbackTimer.Tick += PlaybackTimer_Tick;
                }
                _playbackTimer.Start();

                // Start playback
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();

                playButton.Content = "\u23F8"; // pause icon
                playButton.Tag = "playing";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VOICE] Error playing: {ex.Message}");
                playButton.Content = "\u23F8";
                playButton.Tag = "play";
            }
        }

        private static void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_audioFile == null || _currentSlider == null || _currentTimeText == null) return;
                if (_isUserDragging) return; // don't update while user is dragging

                var current = _audioFile.CurrentTime;
                var total = _audioFile.TotalTime;

                _currentSlider.Value = Math.Min(_currentSlider.Maximum, current.TotalSeconds);
                _currentTimeText.Text = FormatTime(current) + " / " + FormatTime(total);

                // Update the progress track width manually
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
                    catch { /* ignore UI update failures */ }
                }
            }
            catch { }
        }

        private static void SeekAudio(double seconds)
        {
            try
            {
                if (_audioFile == null) return;
                var ts = TimeSpan.FromSeconds(Math.Min(seconds, _audioFile.TotalTime.TotalSeconds));
                _audioFile.CurrentTime = ts;
            }
            catch { }
        }

        private static void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
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

                        // reset progress track width if template exists
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
                        catch { }
                    }

                    if (_currentTimeText != null && _audioFile != null)
                    {
                        _currentTimeText.Text = "00:00 / " + FormatTime(_audioFile.TotalTime);
                    }
                }
                catch { }
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

                // attempt to reset UI progress track before clearing references
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
                catch { }

                _currentMessageId = -1;
                _currentPlayingBorder = null;
                _currentPlayButton = null;
                _currentSlider = null;
                _currentTimeText = null;
            }
            catch { }
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
            catch { }
        }

        private static string FormatTime(TimeSpan t)
        {
            return string.Format("{0:D2}:{1:D2}", (int)t.TotalMinutes, t.Seconds);
        }

        /// <summary>
        /// ? НОВИЙ МЕТОД: Додає контекстне меню для голосових повідомлень
        /// </summary>
        private static void AddVoiceMessageContextMenu(Border messageBorder, MessageDto message, int currentUserId)
        {
            var contextMenu = new ContextMenu();

            if (message.SenderId == currentUserId)
            {
                var deleteItem = new MenuItem
                {
                    Header = DefaultLanguage.DeleteMessage, 
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69))
                };
                deleteItem.Click += async (s, e) => await DeleteVoiceMessageAsync(message);
                contextMenu.Items.Add(deleteItem);

                messageBorder.ContextMenu = contextMenu;
            }
        }

        /// <summary>
        /// ? НОВИЙ МЕТОД: Видалення голосового повідомлення
        /// </summary>
        private static async Task DeleteVoiceMessageAsync(MessageDto message)
        {
            try
            {
                var result = uchat.Pages.MessageBox.ShowQuestion(
                    DefaultLanguage.ConfirmDeleteMessage, 
                    DefaultLanguage.ContactDeleteConfirmTitle); 

                if (result == MessageBoxResult.Yes)
                {
                    bool success = await App.HubService.DeleteMessageAsync(message.Id, message.ChatId);

                    if (!success)
                    {
                        uchat.Pages.MessageBox.ShowError(DefaultLanguage.FailedDeleteMessage, DefaultLanguage.ErrorTitle); 
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting voice message: {ex.Message}");
                uchat.Pages.MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle); 
            }
        }

        /// <summary>
        /// Prefetch audio duration in background and update UI so it doesn't stay "00:00 / 00:00" after creation
        /// </summary>
        private static void PrefetchDuration(MessageDto message, Slider slider, TextBlock timeText)
        {
            // Fire-and-forget task - we just want to update UI when available
            Task.Run(async () =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, "voice.wav");
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        return;

                    // Use AudioFileReader to get duration without starting playback
                    using (var afr = new AudioFileReader(filePath))
                    {
                        var total = afr.TotalTime;

                        // Update UI
                        Application.Current.Dispatcher.Invoke(() =>
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
                                    timeText.Text = "00:00 / " + FormatTime(total);
                                }

                                // Ensure progress track is reset
                                try
                                {
                                    if (slider != null && slider.Template != null)
                                    {
                                        var progressTrack = slider.Template.FindName("ProgressTrack", slider) as Border;
                                        if (progressTrack != null)
                                            progressTrack.Width = 0;
                                    }
                                }
                                catch { }
                            }
                            catch { }
                        });
                    }
                }
                catch { /* ignore failures */ }
            });
        }
    }
}
