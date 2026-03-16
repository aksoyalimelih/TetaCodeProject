namespace TetaCode.Service.Dtos;

/// <summary>
/// OCR ile elde edilen (ve düzenlenmiş olabilen) metni PDF notuna dönüştürmek için kullanılır.
/// </summary>
public class ConvertToPdfDto
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Content { get; set; } = null!;
}
