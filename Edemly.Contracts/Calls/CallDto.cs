namespace Edemly.Contracts.Calls;

public class CallDto
{
    public int Id { get; set; }

    public int ChatId { get; set; }

    public int InitiatorId { get; set; }

    public string? CallUid { get; set; }

    public string? Metadata { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Scope { get; set; } = CallScopes.Direct;

    public string MediaKind { get; set; } = CallMediaKinds.Audio;

    public IReadOnlyList<CallParticipantDto> Participants { get; set; } = Array.Empty<CallParticipantDto>();
}
