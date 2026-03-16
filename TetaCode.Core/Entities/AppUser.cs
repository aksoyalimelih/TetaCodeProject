namespace TetaCode.Core.Entities;

public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}

