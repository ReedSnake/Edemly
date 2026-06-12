#nullable enable

using Edemly.Contracts.Calls;
using Edemly.Contracts.Messages;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class CallSystemMessageRenderer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public void Render(MessageDto message, MessageRenderContext context, bool isHistorical)
        {
            var payload = TryReadPayload(message.Text);
            var title = BuildTitle(payload);
            var details = BuildDetails(payload, message.SentAt);

            var container = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            container.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ResolveBrush("ThemeTextPrimaryBrush", Colors.Black),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            if (!string.IsNullOrWhiteSpace(details))
            {
                container.Children.Add(new TextBlock
                {
                    Text = details,
                    FontSize = 11,
                    Foreground = ResolveBrush("ThemeTextSecondaryBrush", Colors.Gray),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            var border = new Border
            {
                Tag = message.Id,
                Background = ResolveBrush("ThemeSurfaceAltBrush", Color.FromRgb(240, 240, 240)),
                BorderBrush = ResolveBrush("ThemeBorderLightBrush", Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(70, 8, 70, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 360,
                Child = container,
                Opacity = isHistorical ? 0.86 : 1
            };

            context.MessagesPanel.Children.Add(border);
        }

        private static CallMessagePayloadDto? TryReadPayload(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<CallMessagePayloadDto>(text, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildTitle(CallMessagePayloadDto? payload)
        {
            if (payload == null)
            {
                return "Call";
            }

            var media = string.Equals(payload.MediaKind, CallMediaKinds.Video, StringComparison.OrdinalIgnoreCase)
                ? "Video call"
                : "Audio call";

            return NormalizeStatus(payload.Status) switch
            {
                CallLifecycleStatuses.Active => $"{media} in progress",
                CallLifecycleStatuses.Pending => $"{media} started",
                CallLifecycleStatuses.Missed => "Missed call",
                CallLifecycleStatuses.Rejected => "Call rejected",
                _ => $"{media} ended"
            };
        }

        private static string BuildDetails(CallMessagePayloadDto? payload, DateTime fallbackSentAt)
        {
            if (payload == null)
            {
                return fallbackSentAt.ToLocalTime().ToString("HH:mm");
            }

            var startedAt = payload.StartedAt == default
                ? fallbackSentAt
                : payload.StartedAt;

            var status = NormalizeStatus(payload.Status);
            if (status is CallLifecycleStatuses.Ended or CallLifecycleStatuses.Rejected or CallLifecycleStatuses.Missed)
            {
                var duration = payload.DurationSeconds.HasValue
                    ? $" - {FormatDuration(payload.DurationSeconds.Value)}"
                    : string.Empty;

                return $"{startedAt.ToLocalTime():HH:mm}{duration}";
            }

            return $"started {startedAt.ToLocalTime():HH:mm}";
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.Equals(status, CallLifecycleStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Active;
            }

            if (string.Equals(status, CallLifecycleStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Pending;
            }

            if (string.Equals(status, CallLifecycleStatuses.Missed, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Missed;
            }

            if (string.Equals(status, CallLifecycleStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                return CallLifecycleStatuses.Rejected;
            }

            return CallLifecycleStatuses.Ended;
        }

        private static string FormatDuration(long durationSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"mm\:ss");
        }

        private static Brush ResolveBrush(string resourceKey, Color fallbackColor)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallbackColor);
        }
    }
}
