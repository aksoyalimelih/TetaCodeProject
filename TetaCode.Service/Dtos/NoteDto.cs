namespace TetaCode.Service.Dtos;

public class NoteDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? FilePath { get; set; }
    public string? Tags { get; set; }
    public string? Category { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

