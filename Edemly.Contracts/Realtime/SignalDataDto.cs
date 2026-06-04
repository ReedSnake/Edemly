namespace Edemly.Contracts.Realtime;

public sealed class SignalDataDto
{
    public string? CallId { get; set; }
    public int From { get; set; }
    public string? Sdp { get; set; }
}