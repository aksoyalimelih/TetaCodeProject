namespace TetaCode.Core.Entities;

public class Note : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? FilePath { get; set; }
    public string? Tags { get; set; }
    public string? Category { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
}

