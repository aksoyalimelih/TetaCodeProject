using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public interface IAIService
{
    Task<string> SummarizeNoteAsync(string content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuizQuestionDto>> GenerateQuizAsync(string content, CancellationToken cancellationToken = default);
}

