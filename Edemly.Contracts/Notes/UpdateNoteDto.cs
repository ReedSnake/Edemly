using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Notes;

public class UpdateNoteDto
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}