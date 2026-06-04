namespace Edemly.Contracts.Realtime;

public sealed class SignalIceDto
{
    public string? CallId { get; set; }
    public int From { get; set; }
    public string? Candidate { get; set; }
    public string? SdpMid { get; set; }
    public int? SdpMLineIndex { get; set; }
}