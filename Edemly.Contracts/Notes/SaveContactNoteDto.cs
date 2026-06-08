using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Notes;

public sealed class SaveContactNoteDto
{
    [Required]
    public string Content { get; set; } = string.Empty;
}