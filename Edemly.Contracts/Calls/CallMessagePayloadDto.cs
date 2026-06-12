namespace Edemly.Contracts.Calls;

public sealed class CallMessagePayloadDto
{
    public string Schema { get; set; } = "edemly.call.message.v1";

    public int CallId { get; set; }

    public string? CallUid { get; set; }

    public int ChatId { get; set; }

    public int InitiatorId { get; set; }

    public string Scope { get; set; } = CallScopes.Direct;

    public string MediaKind { get; set; } = CallMediaKinds.Audio;

    public string Status { get; set; } = CallLifecycleStatuses.Pending;

    public DateTime StartedAt { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public long? DurationSeconds { get; set; }

    public int? EndedByUserId { get; set; }

    public string? Reason { get; set; }

    public IReadOnlyList<CallParticipantDto> Participants { get; set; } = Array.Empty<CallParticipantDto>();
}
