using Microsoft.AspNetCore.Http;

namespace TetaCode.Service.Dtos;

public class UpdateNoteDto
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public IFormFile? File { get; set; }
    public string? Tags { get; set; }
    public string? Category { get; set; }
}

