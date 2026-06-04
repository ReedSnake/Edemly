namespace Edemly.Contracts.Realtime
{
    public sealed class ProfileUpdatedEventDto
    {
        public int UserId { get; set; }
        public string? PfpUrl { get; set; }
    }
}