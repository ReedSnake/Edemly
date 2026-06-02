#nullable disable
using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Edemly.Client.UI.Helpers
{
    /// <summary>
    /// Клас для запису голосових повідомлень
    /// </summary>
    public class VoiceRecorder : IDisposable
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private string _outputFilePath;
        private bool _isRecording = false;

        public bool IsRecording => _isRecording;
        public event Action<TimeSpan> RecordingTimeUpdated;
        private DateTime _recordingStartTime;
        private System.Timers.Timer _updateTimer;

        /// <summary>
        /// Початок запису
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("[VoiceRecorder] Already recording");
                return;
            }

            try
            {
                _outputFilePath = Path.Combine(Path.GetTempPath(), $"voice_{Guid.NewGuid()}.wav");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Output file: {_outputFilePath}");

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 50
                };

                _writer = new WaveFileWriter(_outputFilePath, _waveIn.WaveFormat);

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                _isRecording = true;
                _recordingStartTime = DateTime.Now;

                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Recording started at {_recordingStartTime}");

                _updateTimer = new System.Timers.Timer(100);
                _updateTimer.Elapsed += (s, e) =>
                {
                    var elapsed = DateTime.Now - _recordingStartTime;
                    RecordingTimeUpdated?.Invoke(elapsed);
                };
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Error starting recording: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Stack trace: {ex.StackTrace}");

                _isRecording = false;
                _waveIn?.Dispose();
                _waveIn = null;
                _writer?.Dispose();
                _writer = null;

                throw;
            }
        }

        /// <summary>
        /// Зупинка запису
        /// </summary>
        public string StopRecording()
        {
            if (!_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("[VoiceRecorder] Not recording");
                return null;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[VoiceRecorder] Stopping recording...");
                
                _isRecording = false;
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _updateTimer = null;

                _waveIn?.StopRecording();
                
                System.Threading.Thread.Sleep(100);
                
                _waveIn?.Dispose();
                _waveIn = null;

                _writer?.Dispose();
                _writer = null;

                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Recording stopped. File: {_outputFilePath}");
                
                if (File.Exists(_outputFilePath))
                {
                    var fileInfo = new FileInfo(_outputFilePath);
                    System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] File size: {fileInfo.Length} bytes");
                    
                    if (fileInfo.Length > 44) 
                    {
                        return _outputFilePath;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[VoiceRecorder] File is too small (empty recording)");
                        return null;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VoiceRecorder] File does not exist");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Error stopping recording: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Скасування запису
        /// </summary>
        public void CancelRecording()
        {
            if (!_isRecording)
                return;

            _isRecording = false;
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _updateTimer = null;

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            _writer?.Dispose();
            _writer = null;

            if (File.Exists(_outputFilePath))
            {
                try
                {
                    File.Delete(_outputFilePath);
                }
                catch { }
            }

            _outputFilePath = null;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (_writer != null && e.BytesRecorded > 0)
                {
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                    _writer.Flush(); 
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceRecorder] Error writing data: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
            }
        }

        public void Dispose()
        {
            CancelRecording();
        }
    }
}
