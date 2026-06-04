using System.ComponentModel.DataAnnotations;

namespace Edemly.Contracts.Notes;

public class DeleteNoteDto
{
    [Required]
    public int Id { get; set; }
}