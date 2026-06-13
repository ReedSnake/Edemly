namespace Edemly.Client.Infrastructure.Realtime
{
    public static class HubMethods
    {
        public const string ReceiveMessage = "ReceiveMessage";
        public const string ReceiveMessageUpdate = "ReceiveMessageUpdate";
        public const string SendNotifyReminder = "SendNotifyReminder";
        public const string ReceiveMessageDelete = "ReceiveMessageDelete";
        public const string GroupCreated = "GroupCreated";
        public const string GroupUpdated = "GroupUpdated";
        public const string UserStatusChanged = "UserStatusChanged";
        public const string ProfileUpdated = "ProfileUpdated";

        public const string IncomingCall = "IncomingCall";
        public const string Calling = "Calling";
        public const string CallAccepted = "CallAccepted";
        public const string CallRejected = "CallRejected";
        public const string CallEnded = "CallEnded";
        public const string CallParticipantUpdated = "CallParticipantUpdated";
        public const string GroupCallUpdated = "GroupCallUpdated";
        public const string Offer = "Offer";
        public const string Answer = "Answer";
        public const string IceCandidate = "IceCandidate";
        public const string AudioChunk = "AudioChunk";

        public const string SendMessage = "SendMessage";
        public const string UpdateMessage = "UpdateMessage";
        public const string DeleteMessage = "DeleteMessage";
        public const string NotifyProfileUpdated = "NotifyProfileUpdated";
        public const string NotifyGroupUpdated = "NotifyGroupUpdated";
        public const string ConfirmRemindingReceived = "ConfirmRemindingReceived";
        public const string GetUserStatus = "GetUserStatus";

        public const string StartCall = "StartCall";
        public const string AcceptCall = "AcceptCall";
        public const string RejectCall = "RejectCall";
        public const string EndCall = "EndCall";
        public const string SetCallMuted = "SetCallMuted";
        public const string SendOffer = "SendOffer";
        public const string SendAnswer = "SendAnswer";
        public const string SendIceCandidate = "SendIceCandidate";
        public const string SendAudioChunk = "SendAudioChunk";
    }

    public static class HubSettings
    {
        public static readonly TimeSpan ShortOperationTimeout = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan StartConnectionTimeout = TimeSpan.FromSeconds(8);
        public static readonly TimeSpan ConnectionCheckInitialDelay = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan ConnectionCheckPeriod = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan WebSocketKeepAliveInterval = TimeSpan.FromSeconds(20);

        public static readonly TimeSpan ReconnectImmediately = TimeSpan.Zero;
        public static readonly TimeSpan ReconnectAfterShortDelay = TimeSpan.FromSeconds(2);
        public static readonly TimeSpan ReconnectAfterMediumDelay = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan ReconnectAfterLongDelay = StartConnectionTimeout;

        public static readonly TimeSpan[] ReconnectDelays =
        {
        ReconnectImmediately,
        ReconnectAfterShortDelay,
        ReconnectAfterMediumDelay,
        ReconnectAfterLongDelay
    };
    }
}
