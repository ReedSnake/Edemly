using Edemly.Server.Application.Common;
using Edemly.Contracts.Realtime;

namespace Edemly.Server.Application.Calls;

public interface ICallService
{
    Task<ServiceResult<CallStartResult>> StartCallAsync(
        int initiatorId,
        int chatId,
        string callUid,
        string? metadata,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallAcceptedResult>> AcceptCallAsync(
        int userId,
        int callId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallRejectedResult>> RejectCallAsync(
        int userId,
        int callId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallEndedResult>> EndCallAsync(
        int userId,
        int callId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallParticipantUpdatedResult>> SetParticipantMutedAsync(
        int userId,
        int callId,
        bool isMuted,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallMissedResult>> MarkPendingCallMissedAsync(
        int callId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CallAudioBroadcastResult>> GetAudioBroadcastRecipientsAsync(
        int senderId,
        int callId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<CallEndedResult>>> EndActiveCallsForUserAsync(
        int userId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<GroupCallEventDto>>> GetActiveGroupCallsForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
