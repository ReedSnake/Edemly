using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Notes;

public class CreateNoteDto
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}