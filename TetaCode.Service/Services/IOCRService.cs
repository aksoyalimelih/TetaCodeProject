using Microsoft.AspNetCore.Http;

namespace TetaCode.Service.Services;

/// <summary>
/// Akıllı belge işleme (OCR) ve PDF üretimi servisi.
/// </summary>
public interface IOCRService
{
    /// <summary>
    /// Görsel dosyasından metin çıkarır (OCR).
    /// </summary>
    Task<string> ExtractTextFromImageAsync(Stream imageStream, string? language = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen metinden profesyonel bir PDF dosyası oluşturur (byte[] döner).
    /// </summary>
    Task<byte[]> GeneratePdfFromTextAsync(string title, string content, CancellationToken cancellationToken = default);
}
