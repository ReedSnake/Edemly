#nullable disable

using System;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageTimeFormatter
    {
        public string Format(DateTime sentAt, bool isHistorical)
        {
            var localTime = sentAt.ToLocalTime();
            return isHistorical
                ? localTime.ToString("dd.MM HH:mm")
                : localTime.ToString("HH:mm");
        }
    }
}
