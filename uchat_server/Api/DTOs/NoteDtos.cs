using System.ComponentModel.DataAnnotations;

namespace uchat_server.Api.DTOs
{
    public class NoteDtos
    {
        public class NoteGetDto
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public int CreatorId { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        public class NoteCreateDto
        {
            [Required]
            public int UserId { get; set; }

            [Required]
            public string Content { get; set; } = string.Empty;
        }

        public class NoteUpdateDto
        {
            [Required]
            public int Id { get; set; }

            [Required]
            public string Content { get; set; } = string.Empty;
        }

        public class NoteDeleteDto
        {
            [Required]
            public int Id { get; set; }
        }
    }
}
