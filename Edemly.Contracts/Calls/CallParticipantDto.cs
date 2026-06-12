namespace Edemly.Contracts.Calls;

public sealed class CallParticipantDto
{
    public int UserId { get; set; }

    public string Status { get; set; } = CallParticipantStatuses.Invited;

    public DateTime? InvitedAt { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public bool IsMuted { get; set; }
}
