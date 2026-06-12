using Edemly.Contracts.Calls;

namespace Edemly.Client.Application.Calls;

public sealed record CallSessionSnapshot(
    CallSessionPhase Phase,
    int? CallId,
    string? CallUid,
    int? ChatId,
    int? InitiatorId,
    int? PeerUserId,
    string Scope,
    string MediaKind,
    IReadOnlyList<CallParticipantDto> Participants,
    string? Reason)
{
    public static CallSessionSnapshot Idle { get; } = new(
        CallSessionPhase.Idle,
        null,
        null,
        null,
        null,
        null,
        CallScopes.Direct,
        CallMediaKinds.Audio,
        Array.Empty<CallParticipantDto>(),
        null);

    public bool HasCall => Phase != CallSessionPhase.Idle && CallId.HasValue;

    public bool IsGroupCall => Scope == CallScopes.Group;
}
