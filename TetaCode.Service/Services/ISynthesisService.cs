using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public interface ISynthesisService
{
    Task<NoteDto> SynthesizeNotesAsync(int userId, IReadOnlyList<int> noteIds, string? title, CancellationToken cancellationToken = default);
}

