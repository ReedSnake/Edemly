using Edemly.Contracts.Calls;

namespace Edemly.Contracts.Realtime;

public sealed class GroupCallEventDto
{
    public int CallId { get; set; }

    public string? CallUid { get; set; }

    public int ChatId { get; set; }

    public int InitiatorId { get; set; }

    public string Scope { get; set; } = CallScopes.Group;

    public string MediaKind { get; set; } = CallMediaKinds.Audio;

    public string Status { get; set; } = CallLifecycleStatuses.Active;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public IReadOnlyList<CallParticipantDto> Participants { get; set; } = Array.Empty<CallParticipantDto>();
}
