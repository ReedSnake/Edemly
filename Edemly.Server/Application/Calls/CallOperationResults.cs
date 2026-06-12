using Edemly.Contracts.Realtime;
using Edemly.Contracts.Messages;

namespace Edemly.Server.Application.Calls;

public sealed record CallStartResult(
    IncomingCallEventDto IncomingCall,
    CallingEventDto Calling,
    IReadOnlyList<int> MemberUserIds,
    IReadOnlyList<int> IncomingRecipientUserIds,
    int InitiatorId,
    bool ExpiresWhenPending,
    GroupCallEventDto? GroupCall,
    MessageDto? SystemMessage,
    bool RejectedAsBusy = false);

public sealed record CallAcceptedResult(
    CallAcceptedEventDto Accepted,
    IReadOnlyList<int> MemberUserIds,
    IReadOnlyList<int> GroupCallRecipientUserIds,
    GroupCallEventDto? GroupCall,
    MessageDto? SystemMessageUpdate);

public sealed record CallRejectedResult(
    CallRejectedNotification Rejected,
    CallEndedNotification? Ended,
    IReadOnlyList<int> MemberUserIds,
    IReadOnlyList<int> RejectedRecipientUserIds,
    bool CallEnded,
    GroupCallEventDto? GroupCall,
    MessageDto? SystemMessageUpdate);

public sealed record CallEndedResult(
    CallEndedNotification Ended,
    IReadOnlyList<int> MemberUserIds,
    bool CallEnded = true,
    GroupCallEventDto? GroupCall = null,
    MessageDto? SystemMessageUpdate = null);

public sealed record CallParticipantUpdatedResult(
    CallParticipantUpdatedEventDto Updated,
    IReadOnlyList<int> RecipientUserIds);

public sealed record CallMissedResult(
    int CallId,
    bool Missed,
    MessageDto? SystemMessageUpdate);

public sealed record CallAudioBroadcastResult(
    IReadOnlyList<int> RecipientUserIds);

public sealed record CallRejectedNotification(
    int CallId,
    int? UserId,
    string? Reason);

public sealed record CallEndedNotification(
    int CallId,
    int UserId,
    string? Reason = null);
