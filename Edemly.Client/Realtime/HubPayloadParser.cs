using System.Diagnostics;
using System.Text.Json;

namespace Edemly.Client.Realtime
{
    public static class HubPayloadParser
    {
        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static T? Deserialize<T>(object? data, string eventName)
        {
            if (data == null)
                return default;

            try
            {
                var json = data is string text
                    ? text
                    : JsonSerializer.Serialize(data);

                Debug.WriteLine($"[HUB RAW] {eventName} payload: {json}");

                return JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HUB] Failed to deserialize {eventName}: {ex}");
                return default;
            }
        }
        public static bool TryDeserialize<T>(object? data, string eventName, out T? result)
        {
            result = Deserialize<T>(data, eventName);
            return result is not null;
        }
    }
}