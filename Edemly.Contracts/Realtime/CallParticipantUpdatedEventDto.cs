using Edemly.Contracts.Calls;

namespace Edemly.Contracts.Realtime;

public sealed class CallParticipantUpdatedEventDto
{
    public int CallId { get; set; }

    public int UserId { get; set; }

    public bool IsMuted { get; set; }

    public IReadOnlyList<CallParticipantDto> Participants { get; set; } = Array.Empty<CallParticipantDto>();
}
