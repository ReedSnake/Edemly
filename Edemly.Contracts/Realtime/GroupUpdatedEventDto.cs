namespace Edemly.Contracts.Realtime
{
    public sealed class GroupUpdatedEventDto
    {
        public int ChatId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
    }
}