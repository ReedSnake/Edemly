namespace Edemly.Contracts.Notes;

public class NoteDto
{
    public int Id { get; set; }

    public int TargetUserId { get; set; }

    public int CreatorId { get; set; }

    public string Content { get; set; } = string.Empty;
}