using Edemly.Contracts.Calls;

namespace Edemly.Contracts.Realtime;

public sealed class CallingEventDto
{
    public int CallId { get; set; }
    public string? CallUid { get; set; }
    public int? ChatId { get; set; }
    public int? InitiatorId { get; set; }
    public string Scope { get; set; } = CallScopes.Direct;
    public string MediaKind { get; set; } = CallMediaKinds.Audio;
    public IReadOnlyList<CallParticipantDto> Participants { get; set; } = Array.Empty<CallParticipantDto>();
}
