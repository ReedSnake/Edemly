namespace Edemly.Client.DTOs
{
    public class CallDto
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int InitiatorId { get; set; }
        public string? CallUid { get; set; }
        public string? Metadata { get; set; }
        public System.DateTime StartedAt { get; set; }
        public System.DateTime? EndedAt { get; set; }
        public string Status { get; set; } = "";
    }
}