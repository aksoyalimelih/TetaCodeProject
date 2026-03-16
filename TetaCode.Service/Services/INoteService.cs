using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public interface INoteService
{
    Task<NoteDto> AddNoteAsync(int userId, CreateNoteDto dto, string webRootPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteDto>> GetAllNotesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync(int userId, string? searchTerm, string? category, CancellationToken cancellationToken = default);
    Task<NoteDto?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<NoteDto?> UpdateNoteAsync(int userId, int id, UpdateNoteDto dto, string webRootPath, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<bool> HardDeleteAsync(int userId, int id, string webRootPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteDto>> GetArchivedNotesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteDto>> SearchArchivedNotesAsync(int userId, string? searchTerm, string? category, CancellationToken cancellationToken = default);
    Task<bool> RestoreAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF dosyası (byte[]) ile yeni not oluşturur (OCR modülü için).
    /// </summary>
    Task<NoteDto> AddNoteWithPdfAsync(int userId, string title, string description, byte[] pdfBytes, string webRootPath, string? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcıdan gelen PDF dosyasını kaydedip metne çevirerek yeni bir not oluşturur.
    /// </summary>
    Task<NoteDto> AddNoteFromPdfAsync(int userId, string title, Stream pdfStream, string webRootPath, CancellationToken cancellationToken = default);
}

