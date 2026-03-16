using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public interface IAudioService
{
    /// <summary>
    /// Ses dosyasını (wav/webm vb.) Gemini ile metne çevirip yeni bir not oluşturur.
    /// </summary>
    Task<NoteDto> CreateNoteFromAudioAsync(
        int userId,
        Stream audioStream,
        string fileName,
        string? title,
        CancellationToken cancellationToken = default);
}

