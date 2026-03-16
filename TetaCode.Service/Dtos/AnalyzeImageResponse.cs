namespace TetaCode.Service.Dtos;

/// <summary>
/// OCR analiz sonucu: görselden okunan metin.
/// </summary>
public class AnalyzeImageResponse
{
    public string Text { get; set; } = string.Empty;
}
