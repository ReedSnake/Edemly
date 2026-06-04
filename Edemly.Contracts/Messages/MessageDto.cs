namespace Edemly.Contracts.Messages
{
    public class MessageDto
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Type { get; set; }
        public string? ContentUrl { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public string? FileName { get; set; }
    }
}