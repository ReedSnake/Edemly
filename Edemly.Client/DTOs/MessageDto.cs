#nullable disable
using System;

namespace Edemly.Client.DTOs
{
    public class MessageDto
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Type { get; set; }  // 0 - Text, 1 - Voice, 2 - Video, 3 - Photo, 4 - File, 5 - Document
        public string ContentUrl { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        
        /// <summary>
        /// Оригінальна назва файлу (для файлів)
        /// </summary>
        public string FileName { get; set; }
    }

    public class MessageCreateDto
    {
        public int ChatId { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Type { get; set; }
        public string ContentUrl { get; set; }
        public string FileName { get; set; }  // Додано для файлів
    }

    public class MessageUpdateDto
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class MessageDeleteDto
    {
        public int MessageId { get; set; }
        public int ChatId { get; set; }
    }
}