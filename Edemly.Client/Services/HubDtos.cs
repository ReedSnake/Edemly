using System;

namespace Edemly.Client.Services
{
    // DTOs used by HubService event handlers
    public class IncomingCallData
    {
        public int CallId { get; set; }
        public string? CallUid { get; set; }
        public int ChatId { get; set; }
        public int InitiatorId { get; set; }
        public string? Metadata { get; set; }
        public DateTime? StartedAt { get; set; }
    }

    public class SignalData
    {
        public string? CallId { get; set; }
        public int From { get; set; }
        public string? Sdp { get; set; }
    }

    public class SignalIce
    {
        public string? CallId { get; set; }
        public int From { get; set; }
        public string? Candidate { get; set; }
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
    }

    internal class CallSimpleEvent { public int CallId { get; set; } public int UserId { get; set; } }
    internal class CallRejectedEvent { public int CallId { get; set; } public int UserId { get; set; } public string? Reason { get; set; } }
}
