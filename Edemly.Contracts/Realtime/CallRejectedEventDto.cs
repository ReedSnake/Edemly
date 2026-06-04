namespace Edemly.Contracts.Realtime;

public sealed class CallRejectedEventDto
{
    public int CallId { get; set; }
    public int UserId { get; set; }
    public string? Reason { get; set; }
}