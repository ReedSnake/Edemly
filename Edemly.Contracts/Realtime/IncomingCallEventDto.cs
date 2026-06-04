namespace Edemly.Contracts.Realtime;

public sealed class IncomingCallEventDto
{
    public int CallId { get; set; }
    public string? CallUid { get; set; }
    public int ChatId { get; set; }
    public int InitiatorId { get; set; }
    public string? Metadata { get; set; }
    public DateTime? StartedAt { get; set; }
}